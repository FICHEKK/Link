using System;
using System.Text;

namespace Networking.Transport
{
    public class PacketWriter
    {
        private static readonly Encoding Encoding = Encoding.UTF8;

        public int Position { get; set; }
        private readonly Packet _packet;

        public PacketWriter(Packet packet) =>
            _packet = packet;

        public void Write(string value) =>
            WriteArray(Encoding.GetBytes(value));

        public unsafe void Write<T>(T value) where T : unmanaged
        {
            var bytesToWrite = sizeof(T);
            EnsureBufferSize(requiredBufferSize: Position + bytesToWrite);
            _packet.Buffer.Write(value, Position);
            Position += bytesToWrite;
        }

        public unsafe void WriteArray<T>(T[] array) where T : unmanaged
        {
            var bytesToWrite = sizeof(int) + array.Length * sizeof(T);
            EnsureBufferSize(requiredBufferSize: Position + bytesToWrite);
            _packet.Buffer.WriteArray(array, Position);
            Position += bytesToWrite;
        }

        public unsafe void WriteSpan<T>(ReadOnlySpan<T> span) where T : unmanaged
        {
            var bytesToWrite = span.Length * sizeof(T);
            EnsureBufferSize(requiredBufferSize: Position + bytesToWrite);
            _packet.Buffer.WriteSpan(span, Position);
            Position += bytesToWrite;
        }

        private void EnsureBufferSize(int requiredBufferSize)
        {
            var currentBuffer = _packet.Buffer;
            if (currentBuffer.Length >= requiredBufferSize) return;

            var expandedBuffer = new byte[Math.Max(currentBuffer.Length * 2, requiredBufferSize)];
            Array.Copy(currentBuffer, expandedBuffer, Position);
            _packet.Buffer = expandedBuffer;
        }
    }
}
