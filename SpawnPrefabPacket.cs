using UnityEngine;

namespace Networking.Transport
{
    /// <summary>
    /// Sent by the server to the client when a prefab should be instantiated.
    /// </summary>
    public class SpawnPrefabPacket : Packet
    {
        public int PrefabId { get; set; }
        public int ObjectId { get; set; }
        public bool IsOwner { get; set; }
        public Vector3 Position { get; set; }

        protected override void WritePayload()
        {
            Write(PrefabId);
            Write(ObjectId);
            Write(IsOwner);
            this.Write(Position);
        }

        protected override void ReadPayload()
        {
            PrefabId = ReadInt();
            ObjectId = ReadInt();
            IsOwner = ReadBool();
            Position = this.ReadVector3();
        }
    }
}
