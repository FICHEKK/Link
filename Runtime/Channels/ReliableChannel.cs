using System;

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
        /// Consists of header type (1 byte) and channel ID (1 byte).
        /// </summary>
        private const int HeaderSize = 2;

        /// <summary>
        /// Defines how many data-bytes can be stored in a single fragment.
        /// </summary>
        private static readonly int BodySize = Packet.MaxSize - HeaderSize - FooterSize;
        
        /// <summary>
        /// Consists of sequence number (2 bytes), fragment index (1 byte) and fragment count (1 byte).
        /// </summary>
        private const int FooterSize = 4;

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

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;
        private ushort _receiveSequenceNumber;
        private bool _isClosed;
        
        public ReliableChannel(Connection connection) =>
            _connection = connection;

        protected override void SendData(Packet packet)
        {
            lock (_pendingPackets)
            {
                if (_isClosed) return;
                
                var dataByteCount = packet.Size - HeaderSize;
                var fragmentCount = dataByteCount / BodySize + (dataByteCount % BodySize != 0 ? 1 : 0);

                if (fragmentCount > byte.MaxValue)
                {
                    Log.Error($"Packet is too big: consists of {fragmentCount} fragments (max is {byte.MaxValue}).");
                    return;
                }

                if (fragmentCount <= 1)
                {
                    SendFragment(packet, fragmentIndex: 0, fragmentCount: 1);
                    return;
                }
                
                for (var fragmentIndex = 0; fragmentIndex < fragmentCount; fragmentIndex++)
                {
                    var fragment = Packet.Get(channelId: packet.Buffer.Bytes[1]).WriteArray
                    (
                        array: packet.Buffer.Bytes,
                        start: HeaderSize + fragmentIndex * BodySize,
                        length: fragmentIndex < fragmentCount - 1 ? BodySize : dataByteCount - fragmentIndex * BodySize,
                        writeLength: false
                    );

                    SendFragment(fragment, (byte) fragmentIndex, (byte) fragmentCount);
                    fragment.Return();
                }
            }
        }
        
        private void SendFragment(Packet fragment, byte fragmentIndex, byte fragmentCount)
        {
            fragment.Write(_localSequenceNumber);
            fragment.Write(fragmentIndex);
            fragment.Write(fragmentCount);
                    
            _connection.Node.Send(fragment, _connection.RemoteEndPoint);
            _pendingPackets[_localSequenceNumber++] = PendingPacket.Get(fragment, reliableChannel: this);
        }

        protected override void ReceiveData(ReadOnlyPacket packet)
        {
            lock (_receivedPackets)
            {
                if (_isClosed) return;
                
                var sequenceNumber = packet.Read<ushort>(position: packet.Size - FooterSize);
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

                while (true)
                {
                    var packetsReassembled = ReceiveIfPossible(_receiveSequenceNumber);
                    if (packetsReassembled == 0) return;
                    
                    _receiveSequenceNumber += packetsReassembled;
                }
            }
        }

        private byte ReceiveIfPossible(ushort sequenceNumber)
        {
            var fragment = _receivedPackets[sequenceNumber];
            if (fragment is null) return 0;
            
            var fragmentIndex = fragment.Read<byte>(offset: fragment.Size - 2);
            var fragmentCount = fragment.Read<byte>(offset: fragment.Size - 1);
            
            var reassembled = ReassembleIfPossible(sequenceNumber, fragmentIndex, fragmentCount);
            if (reassembled is null) return 0;
            
            _connection.Node.Receive(new ReadOnlyPacket(reassembled, position: HeaderSize), _connection.RemoteEndPoint);
            reassembled.Return();
            return fragmentCount;
        }

        private Buffer ReassembleIfPossible(ushort sequenceNumber, byte fragmentIndex, byte fragmentCount)
        {
            // Single fragment packets are a trivial, but the most common case.
            if (fragmentCount == 1) return _receivedPackets[sequenceNumber];
            
            // Range of sequence numbers that contain full packet.
            var startSequenceNumber = sequenceNumber - fragmentIndex;
            var endSequenceNumber = startSequenceNumber + fragmentCount - 1;
            
            // Start iterating from end as newer fragments are more likely to not be received.
            for (var i = endSequenceNumber; i >= startSequenceNumber; i--)
            {
                // If at least one fragment is not received, we cannot reassemble the packet.
                if (_receivedPackets[(ushort) i] is null) return null;
            }
            
            // If we reached this point, all of the fragments have been received, so we can reassemble it.
            var lastFragmentByteCount = _receivedPackets[(ushort) endSequenceNumber].Size - HeaderSize - FooterSize;
            var reassembled = Buffer.OfSize(HeaderSize + (fragmentCount - 1) * BodySize + lastFragmentByteCount);

            // Copy data from all of the full fragments.
            for (var seq = startSequenceNumber; seq < endSequenceNumber; seq++)
            {
                var fullFragment = _receivedPackets[(ushort) seq];
                Array.Copy(fullFragment.Bytes, HeaderSize, reassembled.Bytes, HeaderSize + (seq - startSequenceNumber) * BodySize, BodySize);
                fullFragment.Return();
            }

            // Copy data from the last fragment.
            var lastFragment = _receivedPackets[(ushort) endSequenceNumber];
            Array.Copy(lastFragment.Bytes, HeaderSize, reassembled.Bytes, HeaderSize + (endSequenceNumber - startSequenceNumber) * BodySize, lastFragmentByteCount);
            lastFragment.Return();

            return reassembled;
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
            
                var sequenceNumber = packet.Buffer.Read<ushort>(offset: packet.Size - FooterSize);
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

                var sequenceNumber = packet.Buffer.Read<ushort>(offset: packet.Size - FooterSize);
                _connection.Timeout($"Packet [sequence: {sequenceNumber}] exceeded maximum resend attempts of {MaxResendAttempts}.");
            }
        }

        internal override void Close()
        {
            lock (_pendingPackets)
            lock (_receivedPackets)
            {
                // Prevent any further send/receive operations.
                _isClosed = true;
                
                // There are 0 buffered packets, nothing to release.
                if (IsFirstSequenceNumberGreater(_receiveSequenceNumber, _remoteSequenceNumber)) return;
                
                // Account for sequence number wrapping.
                var sequenceWithWrap = _remoteSequenceNumber >= _receiveSequenceNumber
                    ? _remoteSequenceNumber
                    : _remoteSequenceNumber + BufferSize;

                // Return all of the buffered packets that haven't been received.
                for (int i = _receiveSequenceNumber; i <= sequenceWithWrap; i++)
                    _receivedPackets[i % BufferSize]?.Return();
            }
        }

        public override string ToString() =>
            base.ToString() + $" | Duplicated: {PacketsDuplicated}, {BytesDuplicated} | Resent: {PacketsResent}, {BytesResent} | Packet-loss: {PacketLoss:F3}";
    }
}
