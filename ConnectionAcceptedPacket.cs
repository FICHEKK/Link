namespace Networking.Transport
{
    /// <summary>
    /// Sent by the server to the client if the connection was accepted.
    /// </summary>
    public class ConnectionAcceptedPacket : Packet
    {
        protected override void WritePayload() { }

        protected override void ReadPayload() { }
    }
}
