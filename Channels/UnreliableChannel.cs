using System.Net;
using Networking.Transport.Nodes;

namespace Networking.Transport.Channels
{
    /// <summary>
    /// Fire and forget channel; packet might be lost on the way, can be duplicated and doesn't guarantee ordering.
    /// Useful for inspecting network. Example: ping packets when trying to calculate round-trip-time and packet loss.
    /// </summary>
    public class UnreliableChannel : Channel
    {
        public override byte Id => 0;
        public override int HeaderSizeInBytes => 1;

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
            _node.EnqueuePendingPacket(ConvertDatagramToPacket(datagram, bytesReceived), _remoteEndPoint);

        internal override void ReceiveAcknowledgement(byte[] datagram) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(UnreliableChannel)}'.");
    }
}
