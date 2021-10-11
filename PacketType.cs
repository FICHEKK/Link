namespace Networking.Transport
{
    public enum PacketType : short
    {
        /// <summary>
        /// Sent by the client to the server when client wants to connect.
        /// </summary>
        ConnectionRequest,

        /// <summary>
        /// Sent by the server to the client if the connection was accepted.
        /// </summary>
        ConnectionAccepted,

        /// <summary>
        /// Sent by the server to the client if the new connection request was declined because the server is full.
        /// </summary>
        ConnectionDeclinedServerFull,

        /// <summary>
        /// Sent by the server to the client if the new connection request was declined because the client is already connected.
        /// </summary>
        ConnectionDeclinedAlreadyConnected,

        /// <summary>
        /// Sent by the client to the server in order to calculate the response time.
        /// </summary>
        Ping,

        /// <summary>
        /// Sent by the server to the client as a response to the ping packet.
        /// </summary>
        Pong,

        /// <summary>
        /// Sent by the server to the client when a prefab should be instantiated.
        /// </summary>
        SpawnPrefab,
    }
}
