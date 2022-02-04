using System;

namespace Networking.Transport
{
    public class PacketWriter
    {
        private const int BufferExpansionFactor = 2;

        public int WritePosition { get; set; }
        private readonly Packet _packet;

        public PacketWriter(Packet packet) => _packet = packet;

        public void Write(byte value) => WriteOneByteValue(value);
        public void Write(sbyte value) => WriteOneByteValue((byte) value);
        public void Write(bool value) => WriteOneByteValue((byte) (value ? 1 : 0));

        public void Write(short value) => WriteTwoByteValue(value);
        public void Write(ushort value) => WriteTwoByteValue((short) value);
        public void Write(char value) => WriteTwoByteValue((short) value);

        public void Write(int value) => WriteFourByteValue(value);
        public void Write(uint value) => WriteFourByteValue((int) value);
        public void Write(float value) => WriteFourByteValue(new FourByteStruct {floatValue = value}.intValue);

        public void Write(long value) => WriteEightByteValue(value);
        public void Write(ulong value) => WriteEightByteValue((long) value);
        public void Write(double value) => WriteEightByteValue(new EightByteStruct {doubleValue = value}.longValue);

        private void WriteOneByteValue(byte value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(byte));
            _packet.Buffer[WritePosition++] = value;
        }

        private void WriteTwoByteValue(short value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(short));
#if BIGENDIAN
            _packet.Buffer[WritePosition++] = (byte) (value >> 8);
            _packet.Buffer[WritePosition++] = (byte) value;
#else
            _packet.Buffer[WritePosition++] = (byte) value;
            _packet.Buffer[WritePosition++] = (byte) (value >> 8);
#endif
        }

        private void WriteFourByteValue(int value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(int));
#if BIGENDIAN
            _packet.Buffer[WritePosition++] = (byte) (value >> 24);
            _packet.Buffer[WritePosition++] = (byte) (value >> 16);
            _packet.Buffer[WritePosition++] = (byte) (value >> 8);
            _packet.Buffer[WritePosition++] = (byte) value;
#else
            _packet.Buffer[WritePosition++] = (byte) value;
            _packet.Buffer[WritePosition++] = (byte) (value >> 8);
            _packet.Buffer[WritePosition++] = (byte) (value >> 16);
            _packet.Buffer[WritePosition++] = (byte) (value >> 24);
#endif
        }

        private void WriteEightByteValue(long value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(long));
#if BIGENDIAN
            _packet.Buffer[WritePosition++] = (byte) (value >> 56);
            _packet.Buffer[WritePosition++] = (byte) (value >> 48);
            _packet.Buffer[WritePosition++] = (byte) (value >> 40);
            _packet.Buffer[WritePosition++] = (byte) (value >> 32);
            _packet.Buffer[WritePosition++] = (byte) (value >> 24);
            _packet.Buffer[WritePosition++] = (byte) (value >> 16);
            _packet.Buffer[WritePosition++] = (byte) (value >> 8);
            _packet.Buffer[WritePosition++] = (byte) value;
#else
            _packet.Buffer[WritePosition++] = (byte) value;
            _packet.Buffer[WritePosition++] = (byte) (value >> 8);
            _packet.Buffer[WritePosition++] = (byte) (value >> 16);
            _packet.Buffer[WritePosition++] = (byte) (value >> 24);
            _packet.Buffer[WritePosition++] = (byte) (value >> 32);
            _packet.Buffer[WritePosition++] = (byte) (value >> 40);
            _packet.Buffer[WritePosition++] = (byte) (value >> 48);
            _packet.Buffer[WritePosition++] = (byte) (value >> 56);
#endif
        }

        public void Write(string value)
        {
            Write(value.Length);
            Write(Packet.Encoding.GetBytes(value));
        }

        public void Write(int[] array)
        {
            var bytes = new byte[array.Length * sizeof(int)];
            Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length);
            Write(bytes);
        }

        private void Write(byte[] bytes)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + bytes.Length);

            foreach (var b in bytes)
            {
                _packet.Buffer[WritePosition++] = b;
            }
        }

        private void EnsureBufferSize(int requiredBufferSize)
        {
            var currentBuffer = _packet.Buffer;
            if (currentBuffer.Length >= requiredBufferSize) return;

            var expandedBuffer = new byte[Math.Max(currentBuffer.Length * BufferExpansionFactor, requiredBufferSize)];
            Array.Copy(currentBuffer, expandedBuffer, WritePosition);
            _packet.Buffer = expandedBuffer;
        }
    }
}
