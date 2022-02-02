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
    }
}
