using System;

namespace Networking.Transport
{
    public class PacketWriter
    {
        private const int BufferExpansionFactor = 2;

        public int WritePosition { get; set; }
        private readonly Packet _packet;

        public PacketWriter(Packet packet) => _packet = packet;

        public void Write(byte value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(byte));
            _packet.Buffer.WriteByte(value, WritePosition);
            WritePosition += sizeof(byte);
        }

        public void Write(sbyte value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(sbyte));
            _packet.Buffer.WriteSignedByte(value, WritePosition);
            WritePosition += sizeof(sbyte);
        }

        public void Write(bool value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(bool));
            _packet.Buffer.WriteBool(value, WritePosition);
            WritePosition += sizeof(bool);
        }

        public void Write(short value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(short));
            _packet.Buffer.WriteShort(value, WritePosition);
            WritePosition += sizeof(short);
        }

        public void Write(ushort value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(ushort));
            _packet.Buffer.WriteUnsignedShort(value, WritePosition);
            WritePosition += sizeof(ushort);
        }

        public void Write(char value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(char));
            _packet.Buffer.WriteChar(value, WritePosition);
            WritePosition += sizeof(char);
        }

        public void Write(int value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(int));
            _packet.Buffer.WriteInt(value, WritePosition);
            WritePosition += sizeof(int);
        }

        public void Write(uint value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(uint));
            _packet.Buffer.WriteUnsignedInt(value, WritePosition);
            WritePosition += sizeof(uint);
        }

        public void Write(float value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(float));
            _packet.Buffer.WriteFloat(value, WritePosition);
            WritePosition += sizeof(float);
        }

        public void Write(long value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(long));
            _packet.Buffer.WriteLong(value, WritePosition);
            WritePosition += sizeof(long);
        }

        public void Write(ulong value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(ulong));
            _packet.Buffer.WriteUnsignedLong(value, WritePosition);
            WritePosition += sizeof(ulong);
        }

        public void Write(double value)
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(double));
            _packet.Buffer.WriteDouble(value, WritePosition);
            WritePosition += sizeof(double);
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
