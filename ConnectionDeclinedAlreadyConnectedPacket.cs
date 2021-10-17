namespace Networking.Transport
{
    /// <summary>
    /// Sent by the server to the client if the new connection request was declined because the client is already connected.
    /// </summary>
    public class ConnectionDeclinedAlreadyConnectedPacket : Packet
    {
        protected override void WritePayload() { }

        protected override void ReadPayload() { }
    }
}
