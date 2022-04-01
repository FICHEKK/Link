using System.Collections.Generic;

namespace Networking.Transport.Channels
{
    public class ReliablePacketChannel : ReliableChannel
    {
        private const int ReceiveBufferSize = ushort.MaxValue + 1;
        private const int BitsInAckBitField = sizeof(int) * 8;

        private readonly Dictionary<ushort, PendingPacket> _pendingPackets = new();
        private readonly Packet[] _receivedPackets = new Packet[ReceiveBufferSize];
        private readonly bool _isOrdered;

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;
        private ushort _receiveSequenceNumber;

        public ReliablePacketChannel(Connection connection, bool isOrdered) : base(connection) => _isOrdered = isOrdered;

        protected override (int packetsSent, int bytesSent) ExecuteSend(Packet packet)
        {
            packet.Writer.Write(_localSequenceNumber);
            if (!Connection.Node.Send(packet, Connection.RemoteEndPoint)) return (0, 0);

            lock (_pendingPackets)
            {
                _pendingPackets.Add(_localSequenceNumber++, PendingPacket.Get(packet, reliableChannel: this));
                return (1, packet.Writer.Position);
            }
        }

        protected override void ExecuteReceive(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: bytesReceived - sizeof(ushort));
            UpdateRemoteSequenceNumber(sequenceNumber);
            SendAcknowledgement(sequenceNumber);

            if (_receivedPackets[sequenceNumber] is not null)
            {
                PacketsDuplicated++;
                BytesDuplicated += bytesReceived;
                return;
            }

            _receivedPackets[sequenceNumber] = Packet.From(datagram, bytesReceived);

            if (!_isOrdered)
            {
                Connection.Node.EnqueuePendingPacket(_receivedPackets[sequenceNumber], Connection.RemoteEndPoint);
                return;
            }

            while (_receivedPackets[_receiveSequenceNumber] is not null)
            {
                Connection.Node.EnqueuePendingPacket(_receivedPackets[_receiveSequenceNumber], Connection.RemoteEndPoint);
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
                _receivedPackets[i % ReceiveBufferSize] = null;

            _remoteSequenceNumber = sequenceNumber;
        }

        private void SendAcknowledgement(ushort sequenceNumber)
        {
            var acknowledgeBitField = 0;

            for (var i = 0; i < BitsInAckBitField; i++)
            {
                var wasReceived = _receivedPackets[(ushort) (sequenceNumber - i - 1)] is not null;
                if (wasReceived) acknowledgeBitField |= 1 << i;
            }

            var packet = Packet.Get(HeaderType.Acknowledgement, _isOrdered ? Delivery.Reliable : Delivery.ReliableUnordered);
            packet.Writer.Write(sequenceNumber);
            packet.Writer.Write(acknowledgeBitField);
            Connection.Node.Send(packet, Connection.RemoteEndPoint);
            packet.Return();
        }

        internal override void ReceiveAcknowledgement(byte[] datagram)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            var acknowledgeBitField = datagram.Read<int>(offset: 3);

            lock (_pendingPackets)
            {
                AcknowledgePendingPacket(sequenceNumber);

                for (var i = 0; i < BitsInAckBitField; i++)
                {
                    var isAcknowledged = (acknowledgeBitField & (1 << i)) != 0;
                    if (isAcknowledged) AcknowledgePendingPacket((ushort) (sequenceNumber - i - 1));
                }
            }

            void AcknowledgePendingPacket(ushort seq)
            {
                if (!_pendingPackets.TryGetValue(seq, out var pendingPacket)) return;

                pendingPacket.Acknowledge();
                _pendingPackets.Remove(seq);
            }
        }

        protected override string ExtractPacketInfo(Packet packet)
        {
            var sequenceNumber = packet.Buffer.Read<ushort>(offset: packet.Writer.Position - sizeof(ushort));
            return $"[sequence: {sequenceNumber}]";
        }
    }
}
