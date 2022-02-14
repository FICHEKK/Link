using UnityEngine;

namespace Networking.Transport
{
    public static class PacketReaderExtensions
    {
        public static void ReadTransform(this PacketReader reader, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;

            var bitmask = reader.Read<byte>();

            if ((bitmask & 0x01) != 0) position = reader.Read<Vector3>();
            if ((bitmask & 0x02) != 0) rotation = reader.Read<Quaternion>();
            if ((bitmask & 0x04) != 0) scale = reader.Read<Vector3>();
        }
    }
}
