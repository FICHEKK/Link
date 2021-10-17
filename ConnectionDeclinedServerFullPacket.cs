namespace Networking.Transport
{
    /// <summary>
    /// Sent by the server to the client if the new connection request was declined because the server is full.
    /// </summary>
    public class ConnectionDeclinedServerFullPacket : Packet
    {
        protected override void WritePayload() { }

        protected override void ReadPayload() { }
    }
}
