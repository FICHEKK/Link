using UnityEngine;

namespace Networking.Transport
{
    public static class PacketExtensions
    {
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
    }
}
