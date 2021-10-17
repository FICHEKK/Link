using UnityEngine;

namespace Networking.Transport
{
    public static class PacketExtensions
    {
        public static void Write(this Packet packet, Vector2 vector)
        {
            packet.Write(vector.x);
            packet.Write(vector.y);
        }

        public static Vector2 ReadVector2(this Packet packet)
        {
            var x = packet.ReadFloat();
            var y = packet.ReadFloat();
            return new Vector2(x, y);
        }

        public static void Write(this Packet packet, Vector3 vector)
        {
            packet.Write(vector.x);
            packet.Write(vector.y);
            packet.Write(vector.z);
        }

        public static Vector3 ReadVector3(this Packet packet)
        {
            var x = packet.ReadFloat();
            var y = packet.ReadFloat();
            var z = packet.ReadFloat();
            return new Vector3(x, y, z);
        }

        public static void Write(this Packet packet, Quaternion quaternion)
        {
            packet.Write(quaternion.x);
            packet.Write(quaternion.y);
            packet.Write(quaternion.z);
            packet.Write(quaternion.w);
        }

        public static Quaternion ReadQuaternion(this Packet packet)
        {
            var x = packet.ReadFloat();
            var y = packet.ReadFloat();
            var z = packet.ReadFloat();
            var w = packet.ReadFloat();
            return new Quaternion(x, y, z, w);
        }
    }
}
