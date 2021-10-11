using System;

namespace Networking.Transport
{
    public partial class Packet
    {
        public byte ReadByte() => _buffer[_readPosition++];
        public bool ReadBool() => Read(BitConverter.ToBoolean, sizeof(bool));
        public char ReadChar() => Read(BitConverter.ToChar, sizeof(char));
        public short ReadShort() => Read(BitConverter.ToInt16, sizeof(short));
        public int ReadInt() => Read(BitConverter.ToInt32, sizeof(int));
        public long ReadLong() => Read(BitConverter.ToInt64, sizeof(long));
        public float ReadFloat() => Read(BitConverter.ToSingle, sizeof(float));
        public double ReadDouble() => Read(BitConverter.ToDouble, sizeof(double));

        public string ReadString()
        {
            var stringLength = ReadInt();
            var stringValue = Encoding.GetString(_buffer, _readPosition, stringLength);
            _readPosition += stringLength;
            return stringValue;
        }

        private T Read<T>(Func<byte[], int, T> conversionFunction, int typeSize)
        {
            var value = conversionFunction(_buffer, _readPosition);
            _readPosition += typeSize;
            return value;
        }
    }
}
