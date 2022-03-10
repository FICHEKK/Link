using System.Net;
using Networking.Transport.Nodes;

namespace Networking.Transport.Channels
{
    internal class SequencedChannel : Channel
    {
        private readonly Node _node;
        private readonly EndPoint _remoteEndPoint;

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;

        public SequencedChannel(Node node, EndPoint remoteEndPoint)
        {
            _node = node;
            _remoteEndPoint = remoteEndPoint;
        }

        internal override void Send(Packet packet, bool returnPacketToPool = true)
        {
            packet.Buffer.Write(++_localSequenceNumber, offset: 1);
            _node.Send(packet, _remoteEndPoint, returnPacketToPool);
        }

        internal override void Receive(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            if (!IsFirstSequenceNumberGreater(sequenceNumber, _remoteSequenceNumber)) return;

            _remoteSequenceNumber = sequenceNumber;
            _node.EnqueuePendingPacket(Packet.From(datagram, bytesReceived), _remoteEndPoint);
        }

        internal override void ReceiveAcknowledgement(byte[] datagram) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(SequencedChannel)}'.");
    }
}
