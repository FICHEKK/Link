namespace Networking.Transport
{
    public static class PacketTypeExtensions
    {
        public static ushort Id(this PacketType packetType) =>
            (ushort) packetType;
    }
}
