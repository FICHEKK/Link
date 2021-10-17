namespace Networking.Transport
{
    /// <summary>
    /// Sent by the server to the client when a networked object should be destroyed.
    /// </summary>
    public class DestroyObjectPacket : Packet
    {
        public int ObjectId { get; set; }

        protected override void WritePayload()
        {
            Write(ObjectId);
        }

        protected override void ReadPayload()
        {
            ObjectId = ReadInt();
        }
    }
}
