namespace Networking.Transport
{
    /// <summary>
    /// Sent by the server to the client if the new connection request was declined because the server is full.
    /// </summary>
    public class ConnectionDeclinedPacket : Packet
    {
        public string Reason { get; set; }

        protected override void WritePayload()
        {
            Write(Reason);
        }

        protected override void ReadPayload()
        {
            Reason = ReadString();
        }
    }
}
