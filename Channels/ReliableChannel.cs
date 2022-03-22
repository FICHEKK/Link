using System.Collections.Generic;

namespace Networking.Transport.Channels
{
    public class ReliableChannel : Channel, IReliableChannel
    {
        private const int BufferSize = ushort.MaxValue + 1;
        private const int BitsInAckBitField = sizeof(int) * 8;

        private readonly Connection _connection;
        private readonly Dictionary<ushort, PendingPacket> _sequenceNumberToPendingPacket = new();
        private readonly ReceivedPacket[] _receivedPackets = new ReceivedPacket[BufferSize];

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;
        private ushort _nextReceiveSequenceNumber;

        public double RoundTripTime => _connection.RoundTripTime;

        public ReliableChannel(Connection connection) =>
            _connection = connection;

        // Executed on: Main thread
        protected override (int packetsSent, int bytesSent) ExecuteSend(Packet packet, bool returnPacketToPool)
        {
            packet.Buffer.Write(_localSequenceNumber, offset: 1);
            if (!_connection.Node.Send(packet, _connection.RemoteEndPoint, returnPacketToPool)) return (0, 0);

            lock (_sequenceNumberToPendingPacket)
            {
                _sequenceNumberToPendingPacket.Add(_localSequenceNumber++, PendingPacket.Get(packet, reliableChannel: this));
                return (1, packet.Writer.Position);
            }
        }

        // Executed on: Receive thread
        protected override void ExecuteReceive(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            UpdateRemoteSequenceNumber(sequenceNumber);
            SendAcknowledgement(sequenceNumber);

            var alreadyReceived = _receivedPackets[sequenceNumber].IsReceived;
            if (alreadyReceived) return;

            _receivedPackets[sequenceNumber].Packet = Packet.From(datagram, bytesReceived);

            while (_receivedPackets[_nextReceiveSequenceNumber].IsReceived)
            {
                _connection.Node.EnqueuePendingPacket(_receivedPackets[_nextReceiveSequenceNumber].Packet, _connection.RemoteEndPoint);
                _nextReceiveSequenceNumber++;
            }
        }

        // Executed on: Receive thread
        private void UpdateRemoteSequenceNumber(ushort sequenceNumber)
        {
            if (!IsFirstSequenceNumberGreater(sequenceNumber, _remoteSequenceNumber)) return;

            var sequenceWithWrap = sequenceNumber > _remoteSequenceNumber
                ? sequenceNumber
                : sequenceNumber + BufferSize;

            for (var i = _remoteSequenceNumber + 1; i <= sequenceWithWrap; i++)
                _receivedPackets[i % BufferSize].Packet = null;

            _remoteSequenceNumber = sequenceNumber;
        }

        // Executed on: Receive thread
        private void SendAcknowledgement(ushort sequenceNumber)
        {
            var acknowledgeBitField = 0;

            for (var i = 0; i < BitsInAckBitField; i++)
            {
                var wasReceived = _receivedPackets[(ushort) (sequenceNumber - i - 1)].IsReceived;
                if (wasReceived) acknowledgeBitField |= 1 << i;
            }

            var packet = Packet.Get(HeaderType.Acknowledgement, Delivery.Reliable);
            packet.Writer.Write(sequenceNumber);
            packet.Writer.Write(acknowledgeBitField);
            _connection.Node.Send(packet, _connection.RemoteEndPoint);
        }

        // Executed on: Receive thread
        internal override void ReceiveAcknowledgement(byte[] datagram)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            var acknowledgeBitField = datagram.Read<int>(offset: 3);

            lock (_sequenceNumberToPendingPacket)
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
                if (!_sequenceNumberToPendingPacket.TryGetValue(seq, out var pendingPacket)) return;

                pendingPacket.Acknowledge();
                _sequenceNumberToPendingPacket.Remove(seq);
            }
        }

        // Executed on: Worker thread
        public void ResendPacket(Packet packet)
        {
            _connection.Node.Send(packet, _connection.RemoteEndPoint, returnPacketToPool: false);

            var sequenceNumber = packet.Buffer.Read<ushort>(offset: 1);
            Log.Info($"Re-sent packet {sequenceNumber}.");
        }

        // Executed on: Worker thread
        public void HandleLostPacket(Packet packet)
        {
            _connection.Timeout();

            var sequenceNumber = packet.Buffer.Read<ushort>(offset: 1);
            Log.Info($"Connection timed-out: Packet {sequenceNumber} exceeded maximum resend attempts.");
        }

        private struct ReceivedPacket
        {
            public bool IsReceived => Packet is not null;
            public Packet Packet;
        }
    }
}
