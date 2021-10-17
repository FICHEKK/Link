namespace Networking.Transport
{
    /// <summary>
    /// Sent by the server to the client as a response to the ping packet.
    /// </summary>
    public class PongPacket : Packet
    {
        public int SequenceNumber { get; set; }

        protected override void WritePayload()
        {
            Write(SequenceNumber);
        }

        protected override void ReadPayload()
        {
            SequenceNumber = ReadInt();
        }
    }
}
