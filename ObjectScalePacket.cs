using UnityEngine;

namespace Networking.Transport
{
    public class ObjectScalePacket : Packet
    {
        public int ObjectId { get; set; }
        public Vector3 LocalScale { get; set; }

        protected override void WritePayload()
        {
            Write(ObjectId);
            this.Write(LocalScale);
        }

        protected override void ReadPayload()
        {
            ObjectId = ReadInt();
            LocalScale = this.ReadVector3();
        }
    }
}
