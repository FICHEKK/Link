using System;
using Networking.Exceptions;
using Networking.Synchronization;

namespace Networking.Transport
{
    public class PacketWriter
    {
        private const int BufferExpansionFactor = 2;

        public int WritePosition { get; set; }
        private readonly Packet _packet;

        public PacketWriter(Packet packet) =>
            _packet = packet;

        public void Write(string value) =>
            WriteArray(Packet.Encoding.GetBytes(value));

        public void WriteSerializable<T>(T serializable) where T : INetworkSerializable, new() =>
            serializable.Serialize(this);

        public void WriteSerializableArray<T>(T[] serializableArray) where T : INetworkSerializable, new()
        {
            Write(serializableArray.Length);
            var requiredElementType = typeof(T);

            foreach (var serializable in serializableArray)
            {
                if (serializable.GetType() != requiredElementType)
                    throw new InheritedTypePassedException($"Inherited type '{serializable.GetType()}' could not be serialized as type '{requiredElementType}'.");

                WriteSerializable(serializable);
            }
        }

        public unsafe void Write<T>(T value) where T : unmanaged
        {
            var bytesToWrite = sizeof(T);
            EnsureBufferSize(requiredBufferSize: WritePosition + bytesToWrite);
            _packet.Buffer.Write(value, WritePosition);
            WritePosition += bytesToWrite;
        }

        public unsafe void WriteArray<T>(T[] array) where T : unmanaged
        {
            var bytesToWrite = sizeof(int) + array.Length * sizeof(T);
            EnsureBufferSize(requiredBufferSize: WritePosition + bytesToWrite);
            _packet.Buffer.WriteArray(array, WritePosition);
            WritePosition += bytesToWrite;
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
