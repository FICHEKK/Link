using UnityEngine;

namespace Networking.Transport
{
    public class ObjectRotationPacket : Packet
    {
        public int ObjectId { get; set; }
        public Quaternion LocalRotation { get; set; }

        protected override void WritePayload()
        {
            Write(ObjectId);
            this.Write(LocalRotation);
        }

        protected override void ReadPayload()
        {
            ObjectId = ReadInt();
            LocalRotation = this.ReadQuaternion();
        }
    }
}
