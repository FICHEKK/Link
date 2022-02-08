namespace Networking.Transport
{
    public class PacketReader
    {
        public int ReadPosition { get; set; }
        private readonly Packet _packet;

        public PacketReader(Packet packet) => _packet = packet;

        public byte ReadByte()
        {
            var value = _packet.Buffer.ReadByte(ReadPosition);
            ReadPosition += sizeof(byte);
            return value;
        }

        public sbyte ReadSignedByte()
        {
            var value = _packet.Buffer.ReadSignedByte(ReadPosition);
            ReadPosition += sizeof(sbyte);
            return value;
        }

        public bool ReadBool()
        {
            var value = _packet.Buffer.ReadBool(ReadPosition);
            ReadPosition += sizeof(bool);
            return value;
        }

        public short ReadShort()
        {
            var value = _packet.Buffer.ReadShort(ReadPosition);
            ReadPosition += sizeof(short);
            return value;
        }

        public ushort ReadUnsignedShort()
        {
            var value = _packet.Buffer.ReadUnsignedShort(ReadPosition);
            ReadPosition += sizeof(ushort);
            return value;
        }

        public char ReadChar()
        {
            var value = _packet.Buffer.ReadChar(ReadPosition);
            ReadPosition += sizeof(char);
            return value;
        }

        public int ReadInt()
        {
            var value = _packet.Buffer.ReadInt(ReadPosition);
            ReadPosition += sizeof(int);
            return value;
        }

        public uint ReadUnsignedInt()
        {
            var value = _packet.Buffer.ReadUnsignedInt(ReadPosition);
            ReadPosition += sizeof(uint);
            return value;
        }

        public float ReadFloat()
        {
            var value = _packet.Buffer.ReadFloat(ReadPosition);
            ReadPosition += sizeof(float);
            return value;
        }

        public long ReadLong()
        {
            var value = _packet.Buffer.ReadLong(ReadPosition);
            ReadPosition += sizeof(long);
            return value;
        }

        public ulong ReadUnsignedLong()
        {
            var value = _packet.Buffer.ReadUnsignedLong(ReadPosition);
            ReadPosition += sizeof(ulong);
            return value;
        }

        public double ReadDouble()
        {
            var value = _packet.Buffer.ReadDouble(ReadPosition);
            ReadPosition += sizeof(double);
            return value;
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
