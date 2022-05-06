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

        // TODO - Make this an option that can be tweaked.
        private const int BitsInAckBitField = sizeof(int) * 8;

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
        
        public ReliableChannel(Connection connection, bool isOrdered = true)
        {
            _connection = connection;
            _isOrdered = isOrdered;
        }

        protected override void SendData(Packet packet)
        {
            _connection.Node.Send(packet.Write(_localSequenceNumber), _connection.RemoteEndPoint);
            _pendingPackets[_localSequenceNumber++] = PendingPacket.Get(packet, reliableChannel: this);
        }

        protected override void ReceiveData(ReadOnlyPacket packet)
        {
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
                ReceivePacket(_receivedPackets[sequenceNumber]);
                return;
            }

            while (_receivedPackets[_receiveSequenceNumber] is not null)
            {
                ReceivePacket(_receivedPackets[_receiveSequenceNumber]);
                _receiveSequenceNumber++;
            }

            void ReceivePacket(Buffer buffer)
            {
                _connection.Node.Receive(new ReadOnlyPacket(buffer, position: 2), _connection.RemoteEndPoint);
                buffer.Return();
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
            var ackBitField = 0;

            for (var i = 0; i < BitsInAckBitField; i++)
            {
                var wasReceived = _receivedPackets[(ushort) (sequenceNumber - i - 1)] is not null;
                if (wasReceived) ackBitField |= 1 << i;
            }

            var ack = Packet.Get(HeaderType.Acknowledgement)
                .Write(channelId)
                .Write(sequenceNumber)
                .Write(ackBitField);

            _connection.Node.Send(ack, _connection.RemoteEndPoint);
            ack.Return();
        }

        internal override void ReceiveAcknowledgement(ReadOnlyPacket packet)
        {
            var sequenceNumber = packet.Read<ushort>();
            var ackBitField = packet.Read<int>();

            _pendingPackets[sequenceNumber]?.Acknowledge();
            _pendingPackets[sequenceNumber] = null;

            for (var i = 0; i < BitsInAckBitField; i++)
            {
                var isAcknowledged = (ackBitField & (1 << i)) != 0;
                if (!isAcknowledged) continue;
                
                var seq = (ushort) (sequenceNumber - i - 1);
                _pendingPackets[seq]?.Acknowledge();
                _pendingPackets[seq] = null;
            }
        }

        /// <summary>
        /// Retries sending packet as acknowledgement wasn't received in time.
        /// </summary>
        /// <param name="packet">Packet being resent.</param>
        internal void ResendPacket(Packet packet)
        {
            _connection.Node.Send(packet, _connection.RemoteEndPoint);
            
            var sequenceNumber = packet.Buffer.Read<ushort>(offset: packet.Size - sizeof(ushort));
            Log.Info($"Packet [sequence: {sequenceNumber}] re-sent.");

            PacketsResent++;
            BytesResent += packet.Size;
        }

        /// <summary>
        /// Handles the case of packet exceeding maximum resend attempts.
        /// </summary>
        /// <param name="packet">Packet that was lost.</param>
        internal void HandleLostPacket(Packet packet)
        {
            var sequenceNumber = packet.Buffer.Read<ushort>(offset: packet.Size - sizeof(ushort));
            _connection.Timeout($"Packet [sequence: {sequenceNumber}] exceeded maximum resend attempts of {MaxResendAttempts}.");
        }

        public override string ToString() =>
            base.ToString() + $" | Duplicated: {PacketsDuplicated}, {BytesDuplicated} | Resent: {PacketsResent}, {BytesResent} | Packet-loss: {PacketLoss:F3}";
    }
}
