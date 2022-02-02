namespace Networking.Transport
{
    /// <summary>
    /// Defines all of the packet types used by the networking system.
    /// </summary>
    public enum PacketType : ushort
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
        /// Payload: Reason
        /// </summary>
        ConnectionDeclined,

        /// <summary>
        /// Sent by the client to the server or server to the client when closing the connection.
        /// Client will send this packet to inform server it wants to disconnect.
        /// Server will broadcast this packet when shutting down.
        /// </summary>
        ConnectionClosing,

        /// <summary>
        /// Sent by the server to the client when the client joins and
        /// needs to recreate an already existing networked scene.
        /// Payload: All objects and their variable values
        /// </summary>
        ObjectSceneCreate,

        /// <summary>
        /// Sent by the server to the client each update tick when
        /// there are any scene updates (object variable updates).
        /// Payload: All objects' variable updates
        /// </summary>
        ObjectSceneUpdate,

        /// <summary>
        /// Sent by the server to the client when an object should be spawned.
        /// Payload: PrefabID, ObjectID, Position, Rotation, Scale
        /// </summary>
        ObjectSpawn,

        /// <summary>
        /// Sent by the server to the client to give or take ownership of the specific network object.
        /// Payload: ObjectID, IsOwner
        /// </summary>
        ObjectOwner,

        /// <summary>
        /// Sent by the server to the client to update object's position.
        /// Payload: ObjectID, Position
        /// </summary>
        ObjectPosition,

        /// <summary>
        /// Sent by the server to the client to update object's rotation.
        /// Payload: ObjectID, Rotation
        /// </summary>
        ObjectRotation,

        /// <summary>
        /// Sent by the server to the client to update object's scale.
        /// Payload: ObjectID, Scale
        /// </summary>
        ObjectScale,

        /// <summary>
        /// Sent by the server to the client when a networked object should be destroyed.
        /// Payload: ObjectID
        /// </summary>
        ObjectDestroy,

        /// <summary>
        /// Sent by the client to the server or server to the client when invoking a remote procedure call.
        /// Payload: ObjectID, BehaviourIndex, RpcHash, RpcParameters
        /// </summary>
        Rpc,

        /// <summary>
        /// Sent by the client to the server in order to calculate the response time and check server availability.
        /// Payload: SequenceNumber
        /// </summary>
        Ping,

        /// <summary>
        /// Sent by the server to the client as a response to the ping packet.
        /// Payload: SequenceNumber
        /// </summary>
        Pong,
    }
}