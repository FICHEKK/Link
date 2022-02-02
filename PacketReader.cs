using System;

namespace Networking.Transport
{
    // TODO - Implement faster deserialization using raw byte manipulation.
    public class PacketReader
    {
        public int ReadPosition { get; set; }
        private readonly Packet _packet;

        public PacketReader(Packet packet) => _packet = packet;

        public byte ReadByte() => _packet.Buffer[ReadPosition++];
        public sbyte ReadSignedByte() => (sbyte) ReadByte();
        public bool ReadBool() => ReadByte() != 0;

        public short ReadShort() => Read(BitConverter.ToInt16, sizeof(short));
        public ushort ReadUnsignedShort() => Read(BitConverter.ToUInt16, sizeof(ushort));
        public char ReadChar() => Read(BitConverter.ToChar, sizeof(char));

        public int ReadInt() => Read(BitConverter.ToInt32, sizeof(int));
        public uint ReadUnsignedInt() => Read(BitConverter.ToUInt32, sizeof(uint));
        public float ReadFloat() => Read(BitConverter.ToSingle, sizeof(float));

        public long ReadLong() => Read(BitConverter.ToInt64, sizeof(long));
        public ulong ReadUnsignedLong() => Read(BitConverter.ToUInt64, sizeof(ulong));
        public double ReadDouble() => Read(BitConverter.ToDouble, sizeof(double));

        public string ReadString()
        {
            var stringLength = ReadInt();
            var stringValue = Packet.Encoding.GetString(_packet.Buffer, ReadPosition, stringLength);
            ReadPosition += stringLength;
            return stringValue;
        }

        private T Read<T>(Func<byte[], int, T> conversionFunction, int typeSize)
        {
            var value = conversionFunction(_packet.Buffer, ReadPosition);
            ReadPosition += typeSize;
            return value;
        }
    }
}
