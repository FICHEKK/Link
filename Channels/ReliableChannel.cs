using System.Collections.Generic;
using System.Net;
using Networking.Transport.Nodes;

namespace Networking.Transport.Channels
{
    internal class ReliableChannel : Channel, IReliableChannel
    {
        private const int BufferSize = ushort.MaxValue + 1;
        private const int BitsInAckBitField = sizeof(int) * 8;

        private readonly Node _node;
        private readonly EndPoint _remoteEndPoint;
        private readonly Connection _connection;

        private readonly Dictionary<ushort, PendingPacket> _sequenceNumberToPendingPacket = new();
        private readonly ReceivedPacket[] _receivedPackets = new ReceivedPacket[BufferSize];

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;
        private ushort _nextReceiveSequenceNumber;

        public double RoundTripTime => _connection.RoundTripTime;

        public ReliableChannel(Node node, EndPoint remoteEndPoint, Connection connection)
        {
            _node = node;
            _remoteEndPoint = remoteEndPoint;
            _connection = connection;
        }

        internal override void Send(Packet packet, bool returnPacketToPool = true)
        {
            if (_sequenceNumberToPendingPacket.ContainsKey(_localSequenceNumber))
            {
                // TODO - Disconnect?
                Log.Warning($"Pending packet with sequence number {_localSequenceNumber} already exists.");
                packet.Return();
                return;
            }

            packet.Buffer.Write(_localSequenceNumber, offset: 1);
            _sequenceNumberToPendingPacket[_localSequenceNumber++] = PendingPacket.Get(packet, reliableChannel: this);

            _node.Send(packet, _remoteEndPoint, returnPacketToPool: false);
        }

        public void ResendPacket(Packet packet)
        {
            var sequenceNumber = packet.Buffer.Read<ushort>(offset: 1);

            if (!_sequenceNumberToPendingPacket.ContainsKey(sequenceNumber))
            {
                Log.Warning($"Cannot resend packet that is not pending (sequence number {sequenceNumber}).");
                return;
            }

            _node.Send(packet, _remoteEndPoint, returnPacketToPool: false);
        }

        public void HandleLostPacket(Packet packet)
        {
            _connection.Timeout();
            Log.Info("Connection timed-out: Packet exceeded maximum resend attempts.");
        }

        internal override void Receive(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            UpdateRemoteSequenceNumber(sequenceNumber);
            SendAcknowledgement(sequenceNumber);

            var alreadyReceived = _receivedPackets[sequenceNumber].IsReceived;
            if (alreadyReceived) return;

            _receivedPackets[sequenceNumber].Packet = Packet.From(datagram, bytesReceived);

            while (_receivedPackets[_nextReceiveSequenceNumber].IsReceived)
            {
                _node.EnqueuePendingPacket(_receivedPackets[_nextReceiveSequenceNumber].Packet, _remoteEndPoint);
                _nextReceiveSequenceNumber++;
            }
        }

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
            _node.Send(packet, _remoteEndPoint);
        }

        internal override void ReceiveAcknowledgement(byte[] datagram)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            var acknowledgeBitField = datagram.Read<int>(offset: 3);

            AcknowledgeSentPacket(sequenceNumber);

            for (var i = 0; i < BitsInAckBitField; i++)
            {
                var isAcknowledged = (acknowledgeBitField & (1 << i)) != 0;
                if (isAcknowledged) AcknowledgeSentPacket((ushort) (sequenceNumber - i - 1));
            }
        }

        private void AcknowledgeSentPacket(ushort sequenceNumber)
        {
            if (!_sequenceNumberToPendingPacket.TryGetValue(sequenceNumber, out var pendingPacket)) return;

            pendingPacket.Acknowledge();
            _sequenceNumberToPendingPacket.Remove(sequenceNumber);
        }

        private struct ReceivedPacket
        {
            public bool IsReceived => Packet is not null;
            public Packet Packet;
        }
    }
}
