using System;

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
        public override int HeaderSizeInBytes => 9;

        internal override void PreparePacketForSending(Packet packet)
        {
            throw new NotImplementedException();
        }

        internal override Packet PreparePacketForHandling(byte[] datagram, int bytesReceived)
        {
            throw new NotImplementedException();
        }
    }
}
