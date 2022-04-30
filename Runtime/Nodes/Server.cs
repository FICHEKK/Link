using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

namespace Link.Nodes
{
    /// <summary>
    /// Represents a network node that has one to many relationship with clients.
    /// Server node never communicates with any other server nodes.
    /// </summary>
    public class Server : Node
    {
        /// <summary>
        /// Defines a method that handles incoming packet from a client.
        /// </summary>
        public delegate void PacketHandler(PacketReader reader, EndPoint clientEndPoint);
        
        /// <summary>
        /// Raised each time a packet is received from a client.
        /// </summary>
        public event PacketHandler PacketReceived;
        
        /// <summary>
        /// Invoked each time server starts and begins listening for client connections.
        /// </summary>
        public event Action Started;

        /// <summary>
        /// Invoked each time a new client connects to the server.
        /// </summary>
        public event Action<Connection> ClientConnected;

        /// <summary>
        /// Invoked each time an already connected client disconnects from the server.
        /// </summary>
        public event Action<Connection> ClientDisconnected;

        /// <summary>
        /// Invoked each time server stops and no longer listens for client connections.
        /// </summary>
        public event Action Stopped;

        /// <summary>
        /// Represents a method that is responsible for handling incoming connect packet.
        /// </summary>
        public delegate bool ConnectPacketHandler(PacketReader connectPacketReader, EndPoint clientEndPoint);

        /// <summary>
        /// Validates incoming connection request and decides whether connection should be accepted or not.
        /// </summary>
        public ConnectPacketHandler ConnectionValidator { get; set; }

        /// <summary>
        /// Returns current number of client connections.
        /// </summary>
        public int ConnectionCount => _connections.Count;

        /// <summary>
        /// Returns end-points of currently connected clients.
        /// </summary>
        public IEnumerable<EndPoint> EndPoints => _connections.Keys;

        /// <summary>
        /// Returns connections of currently connected clients.
        /// </summary>
        public IEnumerable<Connection> Connections => _connections.Values;

        /// <summary>
        /// Connections to all of the clients.
        /// </summary>
        private readonly ConcurrentDictionary<EndPoint, Connection> _connections = new();

        /// <summary>
        /// Starts this server and listens for incoming client connections.
        /// </summary>
        /// <param name="port">Port to listen on.</param>
        public void Start(int port)
        {
            Listen(port);
            Started?.Invoke();
        }

        protected override void Consume(PacketReader reader, EndPoint senderEndPoint)
        {
            switch ((HeaderType) reader.Read<byte>())
            {
                case HeaderType.Data:
                    TryGetConnection(senderEndPoint)?.ReceiveData(reader);
                    return;

                case HeaderType.Acknowledgement:
                    TryGetConnection(senderEndPoint)?.ReceiveAcknowledgement(reader);
                    return;

                case HeaderType.Ping:
                    TryGetConnection(senderEndPoint)?.ReceivePing(reader);
                    return;

                case HeaderType.Pong:
                    TryGetConnection(senderEndPoint)?.ReceivePong(reader);
                    return;

                case HeaderType.Connect:
                    HandleConnectPacket(reader, senderEndPoint);
                    return;

                case HeaderType.Disconnect:
                    DisconnectClient(senderEndPoint, "disconnected");
                    return;
                
                case HeaderType.Timeout:
                    DisconnectClient(senderEndPoint, "timed-out");
                    return;

                default:
                    Log.Warning($"Server received invalid packet header from {senderEndPoint}.");
                    return;
            }
        }

        internal override void Receive(PacketReader reader, EndPoint clientEndPoint) => PacketReceived?.Invoke(reader, clientEndPoint);

        private void HandleConnectPacket(PacketReader connectPacketReader, EndPoint senderEndPoint)
        {
            if (_connections.TryGetValue(senderEndPoint, out var connection))
            {
                // Client is connected, but hasn't received the approval.
                connection.ReceiveConnect();
                return;
            }

            if (ConnectionValidator is not null && !ConnectionValidator(connectPacketReader, senderEndPoint))
            {
                Log.Info($"Client connection from {senderEndPoint} was declined as it did not pass the validation test.");
                return;
            }

            Log.Info($"Client from {senderEndPoint} connected.");
            connection = new Connection(node: this, remoteEndPoint: senderEndPoint);
            ConnectionInitializer?.Invoke(connection);
            connection.ReceiveConnect();

            _connections.TryAdd(senderEndPoint, connection);
            ClientConnected?.Invoke(connection);
        }

        /// <summary>
        /// Deliberately disconnects specific client.
        /// </summary>
        public void Kick(EndPoint clientEndPoint) => DisconnectClient(clientEndPoint, "kicked");

        /// <summary>
        /// Disconnects client at specific end-point and logs the reason client was disconnected.
        /// </summary>
        private void DisconnectClient(EndPoint clientEndPoint, string disconnectMethod)
        {
            if (!_connections.TryRemove(clientEndPoint, out var connection)) return;

            Log.Info($"Client from {clientEndPoint} {disconnectMethod}.");
            connection.Dispose();
            ClientDisconnected?.Invoke(connection);
        }

        /// <summary>
        /// Sends a given packet to one client.
        /// </summary>
        public void SendToOne(Packet packet, EndPoint clientEndPoint)
        {
            TryGetConnection(clientEndPoint)?.SendData(packet);
            packet.Return();
        }

        /// <summary>
        /// Sends a given packet to many clients.
        /// </summary>
        public void SendToMany(Packet packet, IEnumerable<EndPoint> clientEndPoints)
        {
            foreach (var clientEndPoint in clientEndPoints)
                TryGetConnection(clientEndPoint)?.SendData(packet);

            packet.Return();
        }

        /// <summary>
        /// Sends a given packet to all clients.
        /// </summary>
        public void SendToAll(Packet packet)
        {
            foreach (var connection in _connections.Values)
                connection.SendData(packet);

            packet.Return();
        }

        private Connection TryGetConnection(EndPoint clientEndPoint)
        {
            if (_connections.TryGetValue(clientEndPoint, out var connection)) return connection;

            Log.Warning($"Could not get connection for client end-point {clientEndPoint}.");
            return null;
        }

        /// <summary>
        /// Stops this server which disconnects all the clients
        /// and prevents any further incoming packets.
        /// </summary>
        public void Stop() => Dispose();

        protected override void Dispose(bool isDisposing)
        {
            foreach (var connection in _connections.Values)
                connection.Dispose();

            _connections.Clear();
            base.Dispose(isDisposing);
            Stopped?.Invoke();
        }
    }
}
