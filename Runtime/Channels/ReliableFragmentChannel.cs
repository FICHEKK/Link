using System.Collections.Generic;

namespace Link.Channels
{
    public class ReliableFragmentChannel : ReliableChannel
    {
        /// <summary>
        /// Maximum number of fragments a packet can consist of.
        /// </summary>
        private const int MaxFragmentCount = ushort.MaxValue >> 1;

        /// <summary>
        /// Defines how many data-bytes can be stored in a single fragment.
        /// </summary>
        private static readonly int BodySize = Packet.MaxSize - HeaderSize - FooterSize;

        /// <summary>
        /// Consists of sequence number (2 bytes) and fragment number (2 bytes).
        /// </summary>
        private const int FooterSize = 4;

        /// <summary>
        /// When the most significant bit is set, it marks that fragment as the last fragment.
        /// </summary>
        private const int LastFragmentBitmask = 1 << 15;

        /// <summary>
        /// Size of the buffer that stores incoming packets.
        /// </summary>
        private const int ReceiveBufferSize = ushort.MaxValue + 1;

        private readonly Dictionary<(ushort sequenceNumber, ushort fragmentNumber), PendingPacket> _pendingPackets = new();
        private readonly FragmentedPacket[] _fragmentedPackets = new FragmentedPacket[ReceiveBufferSize];
        private readonly bool _isOrdered;

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;
        private ushort _receiveSequenceNumber;

        public ReliableFragmentChannel(Connection connection, bool isOrdered) : base(connection) => _isOrdered = isOrdered;

        protected override (int packetsSent, int bytesSent) ExecuteSend(Packet packet)
        {
            var dataByteCount = packet.Size - HeaderSize;
            var fragmentCount = dataByteCount / BodySize + (dataByteCount % BodySize != 0 ? 1 : 0);

            if (fragmentCount == 0)
            {
                Log.Error($"Attempted to send a packet with 0 data bytes on channel '{Name}'.");
                return (0, 0);
            }

            if (fragmentCount > MaxFragmentCount)
            {
                Log.Error($"Packet is too large (consists of {fragmentCount} fragments, while maximum is {MaxFragmentCount} fragments).");
                return (0, 0);
            }

            return fragmentCount == 1 ? SendSingleFragmentPacket(packet) : SendMultiFragmentPacket(packet, fragmentCount, dataByteCount);
        }

        private (int packetsSent, int bytesSent) SendSingleFragmentPacket(Packet packet)
        {
            packet.Write(_localSequenceNumber);
            packet.Write((ushort) LastFragmentBitmask);

            lock (_pendingPackets)
            {
                _pendingPackets.Add((_localSequenceNumber++, LastFragmentBitmask), PendingPacket.Get(packet, reliableChannel: this));
                Connection.Node.Send(packet, Connection.RemoteEndPoint);

                return (1, packet.Size);
            }
        }

        private (int packetsSent, int bytesSent) SendMultiFragmentPacket(Packet packet, int fragmentCount, int dataByteCount)
        {
            lock (_pendingPackets)
            {
                for (var i = 0; i < fragmentCount; i++)
                {
                    var fragmentNumber = i < fragmentCount - 1 ? (ushort) i : (ushort) (i | LastFragmentBitmask);
                    var fragmentLength = i < fragmentCount - 1 ? BodySize : dataByteCount - i * BodySize;
                    var fragment = Packet.Get(HeaderType.Data);

                    fragment.Write(packet.Buffer[1]);
                    fragment.WriteSlice(packet.Buffer, start: HeaderSize + i * BodySize, length: fragmentLength);
                    fragment.Write(_localSequenceNumber);
                    fragment.Write(fragmentNumber);

                    _pendingPackets.Add((_localSequenceNumber, fragmentNumber), PendingPacket.Get(fragment, reliableChannel: this));
                    Connection.Node.Send(fragment, Connection.RemoteEndPoint);

                    fragment.Return();
                }

                _localSequenceNumber++;
                return (fragmentCount, dataByteCount + (HeaderSize + FooterSize) * fragmentCount);
            }
        }

        protected override void ExecuteReceive(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: bytesReceived - FooterSize);
            var fragmentNumber = datagram.Read<ushort>(offset: bytesReceived - FooterSize + sizeof(ushort));
            UpdateRemoteSequenceNumber(sequenceNumber);
            SendAcknowledgement(channelId: datagram[1], sequenceNumber, fragmentNumber);

            var fragmentedPacket = _fragmentedPackets[sequenceNumber];

            if (fragmentedPacket is null)
            {
                fragmentedPacket = new FragmentedPacket(HeaderSize, BodySize, FooterSize);
                _fragmentedPackets[sequenceNumber] = fragmentedPacket;
                _fragmentedPackets[(ushort) (sequenceNumber - ReceiveBufferSize / 2)] = null;
            }

            if (!fragmentedPacket.Add(Packet.From(datagram, bytesReceived, HeaderSize), fragmentNumber & ~LastFragmentBitmask, (fragmentNumber & LastFragmentBitmask) != 0))
            {
                PacketsDuplicated++;
                BytesDuplicated += bytesReceived;
                return;
            }

            if (!_isOrdered)
            {
                if (fragmentedPacket.ReassembledPacket is null) return;

                Connection.Node.EnqueuePendingPacket(fragmentedPacket.ReassembledPacket, Connection.RemoteEndPoint);
                return;
            }

            while (true)
            {
                var nextFragmentedPacket = _fragmentedPackets[_receiveSequenceNumber];
                if (nextFragmentedPacket?.ReassembledPacket is null) break;

                Connection.Node.EnqueuePendingPacket(nextFragmentedPacket.ReassembledPacket, Connection.RemoteEndPoint);
                _receiveSequenceNumber++;
            }
        }

        private void UpdateRemoteSequenceNumber(ushort sequenceNumber)
        {
            if (!IsFirstSequenceNumberGreater(sequenceNumber, _remoteSequenceNumber)) return;

            var sequenceWithWrap = sequenceNumber > _remoteSequenceNumber
                ? sequenceNumber
                : sequenceNumber + ReceiveBufferSize;

            for (var i = _remoteSequenceNumber + 1; i <= sequenceWithWrap; i++)
                _fragmentedPackets[i % ReceiveBufferSize] = null;

            _remoteSequenceNumber = sequenceNumber;
        }

        private void SendAcknowledgement(byte channelId, ushort sequenceNumber, ushort fragmentNumber)
        {
            var packet = Packet.Get(HeaderType.Acknowledgement);
            packet.Write(channelId);
            packet.Write(sequenceNumber);
            packet.Write(fragmentNumber);
            Connection.Node.Send(packet, Connection.RemoteEndPoint);
            packet.Return();
        }

        internal override void ReceiveAcknowledgement(byte[] datagram)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: HeaderSize);
            var fragmentNumber = datagram.Read<ushort>(offset: HeaderSize + sizeof(ushort));

            lock (_pendingPackets)
            {
                var key = (sequenceNumber, fragmentNumber);
                if (!_pendingPackets.TryGetValue(key, out var pendingPacket)) return;

                pendingPacket.Acknowledge();
                _pendingPackets.Remove(key);
            }
        }

        protected override string ExtractPacketInfo(Packet packet)
        {
            var sequenceNumber = packet.Buffer.Read<ushort>(offset: packet.Size - FooterSize);
            var fragmentNumber = packet.Buffer.Read<ushort>(offset: packet.Size - FooterSize + sizeof(ushort));
            return $"[sequence: {sequenceNumber}, fragment: {fragmentNumber}]";
        }
    }
}
