using System;
using System.Net;
using System.Threading.Tasks;
using Networking.Transport.Nodes;

namespace Networking.Transport.Channels
{
    internal class ReliableChannel : Channel
    {
        private const int BufferSize = ushort.MaxValue + 1;
        private const int BitsInAckBitField = sizeof(int) * 8;
        private const int MinResendDelayInMs = 1;
        private const int MaxSendAttempts = 16;

        private readonly Node _node;
        private readonly EndPoint _remoteEndPoint;
        private readonly Connection _connection;

        private readonly SentPacket[] _sentPackets = new SentPacket[BufferSize];
        private readonly ReceivedPacket[] _receivedPackets = new ReceivedPacket[BufferSize];

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;
        private ushort _nextReceiveSequenceNumber;

        public ReliableChannel(Node node, EndPoint remoteEndPoint, Connection connection)
        {
            _node = node;
            _remoteEndPoint = remoteEndPoint;
            _connection = connection;
        }

        internal override void Send(Packet packet, bool returnPacketToPool = true)
        {
            if (!_sentPackets[_localSequenceNumber + 1].IsAcknowledged)
            {
                // TODO - Add to queue of packets that need to be sent, or just disconnect?
                Log.Warning("Send sequence buffer has been exhausted. Packet will not be sent.");
                packet.Return();
                return;
            }

            var sequenceNumber = _localSequenceNumber++;
            packet.Buffer.Write(sequenceNumber, offset: 1);
            _node.Send(packet, _remoteEndPoint, returnPacketToPool: false);

            _sentPackets[sequenceNumber].Packet = packet;
            ResendPacketIfLostAsync(sequenceNumber, sendAttempts: 1);
        }

        private async void ResendPacketIfLostAsync(ushort sequenceNumber, int sendAttempts)
        {
            var resendDelayDuration = _connection.Ping * 2;

            if (resendDelayDuration.TotalMilliseconds < MinResendDelayInMs)
                resendDelayDuration = TimeSpan.FromMilliseconds(MinResendDelayInMs);

            await Task.Delay(resendDelayDuration);

            var sentPacket = _sentPackets[sequenceNumber];
            if (sentPacket.IsAcknowledged) return;

            if (sendAttempts >= MaxSendAttempts)
            {
                Log.Warning($"Packet with sequence number {sequenceNumber} reached maximum send attempts.");
                // TODO - Bad connection, disconnect client?
                sentPacket.Packet.Return();
                return;
            }

            _node.Send(sentPacket.Packet, _remoteEndPoint, returnPacketToPool: false);
            ResendPacketIfLostAsync(sequenceNumber, sendAttempts + 1);
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
            ref var sentPacket = ref _sentPackets[sequenceNumber];
            if (sentPacket.IsAcknowledged) return;

            sentPacket.Packet.Return();
            sentPacket.Packet = null;
        }

        private struct SentPacket
        {
            public bool IsAcknowledged => Packet is null;
            public Packet Packet;
        }

        private struct ReceivedPacket
        {
            public bool IsReceived => Packet is not null;
            public Packet Packet;
        }
    }
}
