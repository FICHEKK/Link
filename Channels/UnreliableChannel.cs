using System.Net;
using Networking.Transport.Nodes;

namespace Networking.Transport.Channels
{
    internal class UnreliableChannel : Channel
    {
        private readonly Node _node;
        private readonly EndPoint _remoteEndPoint;

        public UnreliableChannel(Node node, EndPoint remoteEndPoint)
        {
            _node = node;
            _remoteEndPoint = remoteEndPoint;
        }

        internal override void Send(Packet packet, bool returnPacketToPool = true) =>
            _node.Send(packet, _remoteEndPoint, returnPacketToPool);

        internal override void Receive(byte[] datagram, int bytesReceived) =>
            _node.EnqueuePendingPacket(Packet.From(datagram, bytesReceived), _remoteEndPoint);

        internal override void ReceiveAcknowledgement(byte[] datagram) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(UnreliableChannel)}'.");
    }
}
