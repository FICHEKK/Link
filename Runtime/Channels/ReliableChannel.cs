namespace Link.Channels
{
    /// <summary>
    /// Represents a channel that keeps resending packets until they are either
    /// acknowledged or deemed lost as a result of bad network conditions.
    /// </summary>
    public class ReliableChannel : Channel
    {
        /// <summary>
        /// Size of the pending and received packet buffers.
        /// </summary>
        private const int BufferSize = ushort.MaxValue + 1;

        /// <summary>
        /// Maximum number of resend attempts before deeming the packet as lost.
        /// </summary>
        public int MaxResendAttempts { get; set; } = 15;

        /// <summary>
        /// Time between each consecutive resend is going to get increased by this factor.
        /// Sometimes connection can have a sudden burst of packet loss and trying to
        /// rapidly resend packets is not going to ensure it gets thorough. Waiting for
        /// more and more time gives connection time to stabilize itself.
        /// </summary>
        public double BackoffFactor { get; set; } = 1.2;

        /// <summary>
        /// Minimum possible time duration before resending the packet, in milliseconds.
        /// </summary>
        public int MinResendDelay { get; set; } = 100;

        /// <summary>
        /// The number of additional bytes that will be used in the acknowledgement packet
        /// to acknowledge multiple previous packets.
        /// <br/><br/>
        /// Each acknowledgement packet has specific sequence number that it is acknowledging.
        /// We can then use additional bytes which are going to indicate (1 per bit) whether
        /// previous sequence numbers (relative to the "main" sequence number) were acknowledged.
        /// <br/><br/>
        /// For example, if this value is set to 1, then 8 previous packet acks will be merged
        /// into the acknowledgement packet. This way we can fight packet loss using redundancy.
        /// </summary>
        public int AckBytes { get; set; } = 2;

        /// <summary>
        /// Returns smooth round trip time with added safety margin.
        /// </summary>
        internal double BaseResendDelay => _connection.SmoothRoundTripTime + 4 * _connection.RoundTripTimeDeviation;

        /// <summary>
        /// Returns packet loss percentage (value from 0 to 1) that occured on this channel.
        /// </summary>
        public double PacketLoss => PacketsResent > 0 ? (double) PacketsResent / (PacketsSent + PacketsResent) : 0;

        /// <summary>
        /// Total number of packets resent through this channel.
        /// </summary>
        public long PacketsResent { get; private set; }

        /// <summary>
        /// Total number of bytes resent through this channel.
        /// </summary>
        public long BytesResent { get; private set; }

        /// <summary>
        /// Total number of duplicate packets received on this channel.
        /// </summary>
        public long PacketsDuplicated { get; protected set; }

        /// <summary>
        /// Total number of duplicate bytes received on this channel.
        /// </summary>
        public long BytesDuplicated { get; protected set; }

        private readonly Connection _connection;
        private readonly PendingPacket[] _pendingPackets = new PendingPacket[BufferSize];
        private readonly Buffer[] _receivedPackets = new Buffer[BufferSize];
        private readonly bool _isOrdered;

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;
        private ushort _receiveSequenceNumber;
        private bool _isClosed;
        
        public ReliableChannel(Connection connection, bool isOrdered = true)
        {
            _connection = connection;
            _isOrdered = isOrdered;
        }

        protected override void SendData(Packet packet)
        {
            lock (_pendingPackets)
            {
                if (_isClosed) return;
                
                _connection.Node.Send(packet.Write(_localSequenceNumber), _connection.RemoteEndPoint);
                _pendingPackets[_localSequenceNumber++] = PendingPacket.Get(packet, reliableChannel: this);
            }
        }

        protected override void ReceiveData(ReadOnlyPacket packet)
        {
            lock (_receivedPackets)
            {
                if (_isClosed) return;
                
                var sequenceNumber = packet.Read<ushort>(position: packet.Size - sizeof(ushort));
                UpdateRemoteSequenceNumber(sequenceNumber);
                SendAcknowledgement(packet.ChannelId, sequenceNumber);

                if (_receivedPackets[sequenceNumber] is not null)
                {
                    PacketsDuplicated++;
                    BytesDuplicated += packet.Size;
                    return;
                }

                _receivedPackets[sequenceNumber] = Buffer.Copy(packet.Buffer);
                _receivedPackets[(ushort) (sequenceNumber - BufferSize / 2)] = null;

                if (!_isOrdered)
                {
                    _connection.Node.Receive(new ReadOnlyPacket(_receivedPackets[sequenceNumber], position: 2), _connection.RemoteEndPoint);
                    _receivedPackets[sequenceNumber].Return();
                    return;
                }

                while (_receivedPackets[_receiveSequenceNumber] is not null)
                {
                    _connection.Node.Receive(new ReadOnlyPacket(_receivedPackets[_receiveSequenceNumber], position: 2), _connection.RemoteEndPoint);
                    _receivedPackets[_receiveSequenceNumber++].Return();
                }
            }
        }

        private void UpdateRemoteSequenceNumber(ushort sequenceNumber)
        {
            if (!IsFirstSequenceNumberGreater(sequenceNumber, _remoteSequenceNumber)) return;

            var sequenceWithWrap = sequenceNumber > _remoteSequenceNumber
                ? sequenceNumber
                : sequenceNumber + BufferSize;

            for (var i = _remoteSequenceNumber + 1; i <= sequenceWithWrap; i++)
                _receivedPackets[i % BufferSize] = null;

            _remoteSequenceNumber = sequenceNumber;
        }

        private void SendAcknowledgement(byte channelId, ushort sequenceNumber)
        {
            var ack = Packet.Get(HeaderType.Acknowledgement).Write(channelId).Write(sequenceNumber);

            for (int offset = 0, totalBits = AckBytes * 8; offset < totalBits; offset += 8)
            {
                var ackBitField = 0;

                for (var bit = 0; bit < 8; bit++)
                {
                    var seq = (ushort) (sequenceNumber - offset - bit - 1);
                    if (_receivedPackets[seq] is null) continue;
                    ackBitField |= 1 << bit;
                }

                ack.Write((byte) ackBitField);
            }

            _connection.Node.Send(ack, _connection.RemoteEndPoint);
            ack.Return();
        }

        internal override void ReceiveAcknowledgement(ReadOnlyPacket packet)
        {
            var sequenceNumber = packet.Read<ushort>();
            _pendingPackets[sequenceNumber]?.Acknowledge();
            _pendingPackets[sequenceNumber] = null;

            for (int offset = 0, totalBits = AckBytes * 8; offset < totalBits; offset += 8)
            {
                int ackBitField = packet.Read<byte>();

                for (var bit = 0; bit < 8; bit++)
                {
                    var isAcknowledged = (ackBitField & (1 << bit)) != 0;
                    if (!isAcknowledged) continue;
                    
                    var seq = (ushort) (sequenceNumber - offset - bit - 1);
                    _pendingPackets[seq]?.Acknowledge();
                    _pendingPackets[seq] = null;
                }
            }
        }

        /// <summary>
        /// Retries sending packet as acknowledgement wasn't received in time.
        /// </summary>
        /// <param name="packet">Packet being resent.</param>
        /// <returns><c>true</c> if resend was executed, <c>false</c> if channel is closed.</returns>
        internal bool ResendPacket(Packet packet)
        {
            lock (_pendingPackets)
            {
                if (_isClosed) return false;
                
                _connection.Node.Send(packet, _connection.RemoteEndPoint);
                PacketsResent++;
                BytesResent += packet.Size;
            
                var sequenceNumber = packet.Buffer.Read<ushort>(offset: packet.Size - sizeof(ushort));
                Log.Info($"Packet [sequence: {sequenceNumber}] re-sent.");
                return true;
            }
        }

        /// <summary>
        /// Handles the case of packet exceeding maximum resend attempts.
        /// </summary>
        /// <param name="packet">Packet that was lost.</param>
        internal void HandleLostPacket(Packet packet)
        {
            lock (_pendingPackets)
            {
                if (_isClosed) return;

                var sequenceNumber = packet.Buffer.Read<ushort>(offset: packet.Size - sizeof(ushort));
                _connection.Timeout($"Packet [sequence: {sequenceNumber}] exceeded maximum resend attempts of {MaxResendAttempts}.");
            }
        }

        internal override void Close()
        {
            lock (_pendingPackets)
            lock (_receivedPackets)
            {
                _isClosed = true;
                
                // There is nothing to clean-up as packets don't get buffered.
                if (!_isOrdered) return;
                
                // There are 0 buffered packets.
                if (IsFirstSequenceNumberGreater(_receiveSequenceNumber, _remoteSequenceNumber)) return;
                
                // Account for sequence number wrapping.
                var sequenceWithWrap = _remoteSequenceNumber >= _receiveSequenceNumber ? _remoteSequenceNumber : _remoteSequenceNumber + BufferSize;

                // Return all of the buffered packets that haven't been received.
                for (int i = _receiveSequenceNumber; i <= sequenceWithWrap; i++)
                    _receivedPackets[i % BufferSize]?.Return();
            }
        }

        public override string ToString() =>
            base.ToString() + $" | Duplicated: {PacketsDuplicated}, {BytesDuplicated} | Resent: {PacketsResent}, {BytesResent} | Packet-loss: {PacketLoss:F3}";
    }
}
