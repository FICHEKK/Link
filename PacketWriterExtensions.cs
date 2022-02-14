using UnityEngine;

namespace Networking.Transport
{
    public static class PacketWriterExtensions
    {
        public static void Write(this PacketWriter writer, in Vector3 position, in Quaternion rotation, in Vector3 scale)
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
