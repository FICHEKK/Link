namespace Networking.Transport
{
    /// <summary>
    /// Sent by the client to the server when client wants to connect.
    /// </summary>
    public class ConnectionRequestPacket : Packet
    {
        protected override void WritePayload() { }

        protected override void ReadPayload() { }
    }
}
