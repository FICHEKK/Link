namespace Networking.Transport
{
    /// <summary>
    /// Defines all of the packet types used by the networking system.
    /// </summary>
    public enum PacketType : ushort
    {
        /// <summary>
        /// Sent by the server to the client when the client joins and
        /// needs to recreate an already existing networked scene.
        /// Payload: All objects and their variable values
        /// </summary>
        ObjectSceneCreate,

        /// <summary>
        /// Sent by the server to the client when an object should be spawned.
        /// <br/><br/>
        /// Bitmask is a byte of the following format: 0000 0SRP, where last
        /// 3 bits represent the optional existence of scale, rotation and
        /// position fields respectively.
        /// <br/><br/>
        /// Payload: PrefabID, ObjectID, Bitmask, {Position}, {Rotation}, {Scale}
        /// </summary>
        ObjectSpawn,

        /// <summary>
        /// Sent by the server to the client to give or take ownership of the specific network object.
        /// Payload: ObjectID, IsOwner
        /// </summary>
        ObjectOwner,

        /// <summary>
        /// Sent by the server to the client each update tick when there are any object variable updates.
        /// Payload: ObjectID, variable updates
        /// </summary>
        ObjectUpdate,

        /// <summary>
        /// Sent by the server to the client to update object's transform.
        /// Payload: ObjectID, Transform
        /// </summary>
        ObjectTransform,

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
