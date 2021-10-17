namespace Networking.Transport
{
    /// <summary>
    /// Sent by the client to the server in order to calculate the response time and check server availability.
    /// </summary>
    public class PingPacket : Packet
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
