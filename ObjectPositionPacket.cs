using UnityEngine;

namespace Networking.Transport
{
    public class ObjectPositionPacket : Packet
    {
        public int ObjectId { get; set; }
        public Vector3 LocalPosition { get; set; }

        protected override void WritePayload()
        {
            Write(ObjectId);
            this.Write(LocalPosition);
        }

        protected override void ReadPayload()
        {
            ObjectId = ReadInt();
            LocalPosition = this.ReadVector3();
        }
    }
}
