namespace Networking.Transport.Channels
{
    /// <summary>
    /// Fire and forget channel; packet might be lost on the way, can be duplicated and doesn't guarantee ordering.
    /// Useful for inspecting network. Example: ping packets when trying to calculate round-trip-time and packet loss.
    /// </summary>
    public class UnreliableChannel : Channel
    {
        public override byte Id => (byte) HeaderType.UnreliableData;
        public override int HeaderSizeInBytes => 1;

        internal override void PreparePacketForSending(Packet packet)
        {
            // Empty.
        }

        internal override Packet PreparePacketForHandling(byte[] datagram, int bytesReceived) =>
            ConvertDatagramToPacket(datagram, bytesReceived);
    }
}
