using UnityEngine;

namespace Networking.Transport
{
    public static class PacketReaderExtensions
    {
        public static Vector2 ReadVector2(this PacketReader reader)
        {
            var x = reader.ReadFloat();
            var y = reader.ReadFloat();
            return new Vector2(x, y);
        }

        public static Vector3 ReadVector3(this PacketReader reader)
        {
            var x = reader.ReadFloat();
            var y = reader.ReadFloat();
            var z = reader.ReadFloat();
            return new Vector3(x, y, z);
        }

        public static Quaternion ReadQuaternion(this PacketReader reader)
        {
            var x = reader.ReadFloat();
            var y = reader.ReadFloat();
            var z = reader.ReadFloat();
            var w = reader.ReadFloat();
            return new Quaternion(x, y, z, w);
        }

        public static Color ReadColor(this PacketReader reader)
        {
            var r = reader.ReadFloat();
            var g = reader.ReadFloat();
            var b = reader.ReadFloat();
            var a = reader.ReadFloat();
            return new Color(r, g, b, a);
        }
    }
}
