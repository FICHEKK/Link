using System;

namespace Networking.Transport
{
    public class PacketWriter
    {
        private const int BufferExpansionFactor = 2;

        public int WritePosition { get; set; }
        private readonly Packet _packet;

        public PacketWriter(Packet packet) => _packet = packet;

        public unsafe void Write<T>(T value) where T : unmanaged
        {
            EnsureBufferSize(requiredBufferSize: WritePosition + sizeof(T));
            _packet.Buffer.Write(value, WritePosition);
            WritePosition += sizeof(T);
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
