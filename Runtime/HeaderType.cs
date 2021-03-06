namespace Link
{
    /// <summary>
    /// Defines how packet should be handled on the receiving side.
    /// Header is always inserted as the first byte in the packet.
    /// </summary>
    internal enum HeaderType : byte
    {
        /// <summary>
        /// Marks packet as a connection request packet. This type of packet is always sent by client
        /// to server when establishing a virtual connection. This packet is always the first packet
        /// and is unreliable by default - as no virtual connection has yet been established.
        /// </summary>
        Connect,

        /// <summary>
        /// Sent by the server to client as a response to <see cref="Connect"/> packet. Client
        /// that receives packet of this type can safely start sending data.
        /// </summary>
        ConnectApproved,

        /// <summary>
        /// Marks this packet as a ping packet. This packet type is used for measuring
        /// connection latency and serves as a keep-alive packet.
        /// </summary>
        Ping,

        /// <summary>
        /// Marks this packet as a pong packet, which acts as a response packet to the
        /// <see cref="Ping"/> packet.
        /// </summary>
        Pong,

        /// <summary>
        /// Indicates that this packet contains data.
        /// </summary>
        Data,

        /// <summary>
        /// Indicates that this packet is an acknowledgement packet which is used in reliable
        /// channels to indicate that specific packet or a set of packets have been received.
        /// </summary>
        Acknowledgement,

        /// <summary>
        /// Marks this packet as a disconnect packet which means that the sender has closed its
        /// side of the connection and will no longer send or receive any packets.
        /// </summary>
        Disconnect,
        
        /// <summary>
        /// Marks this packet as a timeout packet which indicates that a connection has timed-out.
        /// This is a self-referential packet, meaning that it does not come from the remote
        /// end-point, but rather is sent by the local end-point when connection times-out.
        /// </summary>
        Timeout,
    }
}
