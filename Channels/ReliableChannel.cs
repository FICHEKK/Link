using System;
using System.Net;
using Networking.Transport.Nodes;

namespace Networking.Transport.Channels
{
    /// <summary>
    /// Each packet is guaranteed to be delivered (unless the connection is faulty), won't be duplicated and will arrive in order.
    /// This is the most expensive delivery method as every packet needs to be acknowledged by the receiving end-point.
    /// Any data that must be delivered and be in order should use this delivery method (example: chat messages).
    /// </summary>
    public class ReliableChannel : Channel
    {
        public override byte Id => (byte) HeaderType.ReliableData;
        public override int HeaderSizeInBytes => 3;

        private readonly Node _node;
        private readonly EndPoint _remoteEndPoint;

        public ReliableChannel(Node node, EndPoint remoteEndPoint)
        {
            _node = node;
            _remoteEndPoint = remoteEndPoint;
        }

        internal override void Send(Packet packet, bool returnPacketToPool = true) =>
            throw new NotImplementedException();

        internal override void Receive(byte[] datagram, int bytesReceived) =>
            throw new NotImplementedException();

        internal override void ReceiveAcknowledgement(byte[] datagram) =>
            throw new NotImplementedException();
    }
}
