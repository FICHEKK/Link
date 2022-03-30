using System.Collections.Generic;

namespace Networking.Transport.Channels
{
    public class ReliableChannel : ReliableChannelBase
    {
        private const int ReceiveBufferSize = ushort.MaxValue + 1;
        private const int BitsInAckBitField = sizeof(int) * 8;

        private readonly Dictionary<ushort, PendingPacket> _pendingPackets = new();
        private readonly Packet[] _receivedPackets = new Packet[ReceiveBufferSize];

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;
        private ushort _receiveSequenceNumber;

        public ReliableChannel(Connection connection) : base(connection) { }

        protected override (int packetsSent, int bytesSent) ExecuteSend(Packet packet, bool returnPacketToPool)
        {
            packet.Buffer.Write(_localSequenceNumber, offset: 1);
            if (!Connection.Node.Send(packet, Connection.RemoteEndPoint, returnPacketToPool)) return (0, 0);

            lock (_pendingPackets)
            {
                _pendingPackets.Add(_localSequenceNumber++, PendingPacket.Get(packet, reliableChannel: this));
                return (1, packet.Writer.Position);
            }
        }

        protected override void ExecuteReceive(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            UpdateRemoteSequenceNumber(sequenceNumber);
            SendAcknowledgement(sequenceNumber);

            if (_receivedPackets[sequenceNumber] is not null) return;
            _receivedPackets[sequenceNumber] = Packet.From(datagram, bytesReceived);

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

            var packet = Packet.Get(HeaderType.Acknowledgement, Delivery.Reliable);
            packet.Writer.Write(sequenceNumber);
            packet.Writer.Write(acknowledgeBitField);
            Connection.Node.Send(packet, Connection.RemoteEndPoint);
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
            var sequenceNumber = packet.Buffer.Read<ushort>(offset: 1);
            return $"[sequence: {sequenceNumber}]";
        }
    }
}
