using System;
using Networking.Serialization;
using UnityEngine;

namespace Networking.Transport
{
    public static class PacketWriterExtensions
    {
        public static void WriteSerializableArray<T>(this PacketWriter writer, T[] serializableArray) where T : INetworkSerializable, new()
        {
            writer.Write(serializableArray.Length);
            var requiredElementType = typeof(T);

            foreach (var serializable in serializableArray)
            {
                if (serializable.GetType() != requiredElementType)
                    throw new InvalidOperationException($"Inherited type '{serializable.GetType()}' could not be serialized as type '{requiredElementType}'.");

                writer.WriteSerializable(serializable);
            }
        }

        public static void WriteSerializable<T>(this PacketWriter writer, T serializable) where T : INetworkSerializable, new() =>
            serializable.Serialize(writer);

        public static void WriteTransform(this PacketWriter writer, in Vector3 position, in Quaternion rotation, in Vector3 scale)
        {
            byte bitmask = 0x00;

            var startWritePosition = writer.WritePosition;
            writer.Write(bitmask);

            if (position != Vector3.zero)
            {
                bitmask |= 0x01;
                writer.Write(position);
            }

            if (rotation != Quaternion.identity)
            {
                bitmask |= 0x02;
                writer.Write(rotation);
            }

            if (scale != Vector3.one)
            {
                bitmask |= 0x04;
                writer.Write(scale);
            }

            var currentWritePosition = writer.WritePosition;
            writer.WritePosition = startWritePosition;
            writer.Write(bitmask);
            writer.WritePosition = currentWritePosition;
        }
    }
}
