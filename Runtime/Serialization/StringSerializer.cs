using System;

namespace Link.Serialization
{
    public class StringSerializer : ISerializer<string>
    {
        private static readonly ISerializer<byte> ByteSerializer = Serializers.Get<byte>();
        private static readonly ISerializer<byte[]> ByteArraySerializer = Serializers.Get<byte[]>();
        
        public void Write(Packet packet, string @string)
        {
            if (@string is null)
                throw new InvalidOperationException("Cannot write null string to a packet.");

            if (@string.Length == 0)
            {
                ByteSerializer.Write(packet, 0);
            }
            else
            {
                ByteArraySerializer.Write(packet, Packet.Encoding.GetBytes(@string));
            }
        }

        public string Read(ReadOnlyPacket packet)
        {
            var stringByteCount = packet.Buffer.ReadVarInt(out _);

            if (stringByteCount == 0)
                return string.Empty;

            if (packet.UnreadBytes < stringByteCount)
                throw new InvalidOperationException("Could not read string (out-of-bounds bytes).");

            var @string = Packet.Encoding.GetString(packet.Buffer.Bytes, packet.Buffer.ReadPosition, stringByteCount);
            packet.Buffer.ReadPosition += stringByteCount;
            return @string;
        }
    }
}
