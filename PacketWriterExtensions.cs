using UnityEngine;

namespace Networking.Transport
{
    public static class PacketWriterExtensions
    {
        public static void Write(this PacketWriter writer, Vector2 vector)
        {
            writer.Write(vector.x);
            writer.Write(vector.y);
        }

        public static void Write(this PacketWriter writer, Vector3 vector)
        {
            writer.Write(vector.x);
            writer.Write(vector.y);
            writer.Write(vector.z);
        }

        public static void Write(this PacketWriter writer, Quaternion quaternion)
        {
            writer.Write(quaternion.x);
            writer.Write(quaternion.y);
            writer.Write(quaternion.z);
            writer.Write(quaternion.w);
        }

        public static void Write(this PacketWriter writer, Color color)
        {
            writer.Write(color.r);
            writer.Write(color.g);
            writer.Write(color.b);
            writer.Write(color.a);
        }

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
