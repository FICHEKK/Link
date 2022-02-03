using Networking.Transport.Conversion;

namespace Networking.Transport
{
    public class PacketReader
    {
        public int ReadPosition { get; set; }
        private readonly Packet _packet;

        public PacketReader(Packet packet) => _packet = packet;

        public sbyte ReadSignedByte() => (sbyte) ReadByte();
        public bool ReadBool() => ReadByte() != 0;

        public ushort ReadUnsignedShort() => (ushort) ReadShort();
        public char ReadChar() => (char) ReadShort();

        public uint ReadUnsignedInt() => (uint) ReadInt();
        public float ReadFloat() => new FourByteStruct {intValue = ReadInt()}.floatValue;

        public ulong ReadUnsignedLong() => (ulong) ReadLong();
        public double ReadDouble() => new EightByteStruct {longValue = ReadLong()}.doubleValue;

        public byte ReadByte() => _packet.Buffer[ReadPosition++];

        public short ReadShort()
        {
#if BIGENDIAN
            var b0 = (int) _packet.Buffer[ReadPosition++];
            var b1 = (int) _packet.Buffer[ReadPosition++];
            return (short) (b0 << 8 | b1);
#else
            var b0 = (int) _packet.Buffer[ReadPosition++];
            var b1 = (int) _packet.Buffer[ReadPosition++];
            return (short) (b0 | b1 << 8);
#endif
        }

        public int ReadInt()
        {
#if BIGENDIAN
            var b0 = (int) _packet.Buffer[ReadPosition++];
            var b1 = (int) _packet.Buffer[ReadPosition++];
            var b2 = (int) _packet.Buffer[ReadPosition++];
            var b3 = (int) _packet.Buffer[ReadPosition++];
            return b0 << 24 | b1 << 16 | b2 << 8 | b3;
#else
            var b0 = (int) _packet.Buffer[ReadPosition++];
            var b1 = (int) _packet.Buffer[ReadPosition++];
            var b2 = (int) _packet.Buffer[ReadPosition++];
            var b3 = (int) _packet.Buffer[ReadPosition++];
            return b0 | b1 << 8 | b2 << 16 | b3 << 24;
#endif
        }

        public long ReadLong()
        {
#if BIGENDIAN
            var b0 = (long) _packet.Buffer[ReadPosition++];
            var b1 = (long) _packet.Buffer[ReadPosition++];
            var b2 = (long) _packet.Buffer[ReadPosition++];
            var b3 = (long) _packet.Buffer[ReadPosition++];
            var b4 = (long) _packet.Buffer[ReadPosition++];
            var b5 = (long) _packet.Buffer[ReadPosition++];
            var b6 = (long) _packet.Buffer[ReadPosition++];
            var b7 = (long) _packet.Buffer[ReadPosition++];
            return b0 << 56 | b1 << 48 | b2 << 40 | b3 << 32 | b4 << 24 | b5 << 16 | b6 << 8 | b7;
#else
            var b0 = (long) _packet.Buffer[ReadPosition++];
            var b1 = (long) _packet.Buffer[ReadPosition++];
            var b2 = (long) _packet.Buffer[ReadPosition++];
            var b3 = (long) _packet.Buffer[ReadPosition++];
            var b4 = (long) _packet.Buffer[ReadPosition++];
            var b5 = (long) _packet.Buffer[ReadPosition++];
            var b6 = (long) _packet.Buffer[ReadPosition++];
            var b7 = (long) _packet.Buffer[ReadPosition++];
            return b0 | b1 << 8 | b2 << 16 | b3 << 24 | b4 << 32 | b5 << 40 | b6 << 48 | b7 << 56;
#endif
        }

        public string ReadString()
        {
            var stringLength = ReadInt();
            var stringValue = Packet.Encoding.GetString(_packet.Buffer, ReadPosition, stringLength);
            ReadPosition += stringLength;
            return stringValue;
        }
    }
}
