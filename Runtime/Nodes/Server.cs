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
        /// Defines a method that handles incoming data-packet from a client.
        /// </summary>
        /// <param name="server">Server that has received the packet.</param>
        /// <param name="packet">Packet that was received.</param>
        /// <param name="clientEndPoint">End-point of a client that has sent the packet.</param>
        public delegate void PacketHandler(Server server, ReadOnlyPacket packet, EndPoint clientEndPoint);
        
        /// <summary>
        /// Represents a method that is responsible for handling incoming connect packet.
        /// </summary>
        public delegate bool ConnectPacketHandler(ReadOnlyPacket connectPacket, EndPoint clientEndPoint);

        /// <summary>
        /// Validates incoming connection request and decides whether connection should be accepted or not.
        /// </summary>
        public ConnectPacketHandler? ConnectionValidator { get; set; }
        
        /// <summary>
        /// Invoked each time server starts and begins listening for client connections.
        /// </summary>
        public event EventHandler<StartedEventArgs>? Started;

        /// <summary>
        /// Invoked each time a new client connects to the server.
        /// </summary>
        public event EventHandler<ClientConnectedEventArgs>? ClientConnected;

        /// <summary>
        /// Invoked each time an already connected client disconnects from the server.
        /// </summary>
        public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

        /// <summary>
        /// Invoked each time server stops and no longer listens for client connections.
        /// </summary>
        public event EventHandler<StoppedEventArgs>? Stopped;

        /// <summary>
        /// Returns current number of client connections.
        /// </summary>
        public int ConnectionCount => _connections.Count;
        
        /// <summary>
        /// Returns maximum allowed number of client connections.
        /// </summary>
        public int MaxConnectionCount { get; private set; }

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
        /// Maps packet types to their handlers.
        /// </summary>
        private readonly Dictionary<ushort, PacketHandler> _packetIdToHandler = new();

        /// <summary>
        /// Starts this server and listens for incoming client connections.
        /// </summary>
        /// <param name="port">Port to listen on.</param>
        /// <param name="maxConnectionCount">
        /// Maximum allowed number of simultaneous client connections.
        /// If set to a negative value, there is no connection limit.
        /// </param>
        public void Start(ushort port, int maxConnectionCount = -1)
        {
            MaxConnectionCount = maxConnectionCount;
            Listen(port);
            Started?.Invoke(new StartedEventArgs(this));
        }

        protected override void Consume(ReadOnlyPacket packet, EndPoint senderEndPoint)
        {
            switch ((HeaderType) packet.Read<byte>())
            {
                case HeaderType.Data:
                    TryGetConnection(senderEndPoint)?.ReceiveData(packet);
                    return;

                case HeaderType.Acknowledgement:
                    TryGetConnection(senderEndPoint)?.ReceiveAcknowledgement(packet);
                    return;

                case HeaderType.Ping:
                    TryGetConnection(senderEndPoint)?.ReceivePing(packet);
                    return;

                case HeaderType.Pong:
                    TryGetConnection(senderEndPoint)?.ReceivePong(packet);
                    return;

                case HeaderType.Connect:
                    HandleConnectPacket(packet, senderEndPoint);
                    return;

                case HeaderType.Disconnect:
                    DisconnectClient(senderEndPoint, DisconnectCause.ClientLogic);
                    return;
                
                case HeaderType.Timeout:
                    DisconnectClient(senderEndPoint, DisconnectCause.Timeout);
                    return;

                default:
                    Log.Warning($"Server received invalid packet header from {senderEndPoint}.");
                    return;
            }
        }

        private void HandleConnectPacket(ReadOnlyPacket connectPacket, EndPoint senderEndPoint)
        {
            if (connectPacket.Size != Packet.MaxSize)
            {
                Log.Warning($"Received connect packet that contains no padding (size: {connectPacket.Size} bytes).");
                return;
            }
            
            if (_connections.TryGetValue(senderEndPoint, out var connection))
            {
                SendConnectApproved();
                return;
            }

            if (MaxConnectionCount >= 0 && ConnectionCount >= MaxConnectionCount)
            {
                Log.Info($"Client connection from {senderEndPoint} was declined as server is full.");
                return;
            }

            if (ConnectionValidator is not null && !ConnectionValidator(connectPacket, senderEndPoint))
            {
                Log.Info($"Client connection from {senderEndPoint} was declined as it did not pass the validation test.");
                return;
            }

            Log.Info($"Client from {senderEndPoint} connected.");
            connection = new Connection(node: this, remoteEndPoint: senderEndPoint, initialState: Connection.State.Connected);
            ConnectionInitializer?.Invoke(connection);
            
            connection.ReceiveConnectApproved();
            SendConnectApproved();

            _connections.TryAdd(senderEndPoint, connection);
            ClientConnected?.Invoke(new ClientConnectedEventArgs(this, connection));

            void SendConnectApproved()
            {
                var connectApprovedPacket = Packet.Get(HeaderType.ConnectApproved);
                Send(connectApprovedPacket, senderEndPoint);
                connectApprovedPacket.Return();
            }
        }

        /// <summary>
        /// Deliberately disconnects specific client.
        /// </summary>
        public void Kick(EndPoint clientEndPoint) => DisconnectClient(clientEndPoint, DisconnectCause.ServerLogic);

        /// <summary>
        /// Disconnects client at specific end-point and logs the cause of the disconnection.
        /// </summary>
        private void DisconnectClient(EndPoint clientEndPoint, DisconnectCause cause)
        {
            if (!_connections.TryRemove(clientEndPoint, out var connection)) return;

            Log.Info($"Client from {clientEndPoint} disconnected (cause: {cause}).");
            connection.Close();
            ClientDisconnected?.Invoke(new ClientDisconnectedEventArgs(this, connection, cause));
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

        private Connection? TryGetConnection(EndPoint clientEndPoint)
        {
            if (TryGetConnection(clientEndPoint, out var connection)) return connection;

            Log.Warning($"Could not get connection for client end-point {clientEndPoint}.");
            return null;
        }
        
        internal override void Receive(Buffer packet, EndPoint senderEndPoint)
        {
            var packetId = packet.Read<ushort>(offset: 2);

            if (!_packetIdToHandler.TryGetValue(packetId, out var packetHandler))
            {
                Log.Warning($"{nameof(Server)} could not handle packet (ID = {packetId}) as there is no handler for it.");
                return;
            }

            packetHandler(this, new ReadOnlyPacket(packet, start: Packet.HeaderSize), senderEndPoint);
        }
        
        /// <summary>
        /// Adds a packet handler for a specific packet ID.
        /// </summary>
        /// <exception cref="InvalidOperationException">If given packet ID already has a handler.</exception>
        public void AddHandler(PacketHandler packetHandler, ushort packetId = ushort.MaxValue)
        {
            if (_packetIdToHandler.ContainsKey(packetId))
                throw new InvalidOperationException($"{nameof(Server)} already has a handler for packet ID {packetId}.");
            
            _packetIdToHandler.Add(packetId, packetHandler);
        }
        
        /// <summary>
        /// Removes a packet handler that is associated with a specific packet ID.
        /// </summary>
        /// <returns><c>true</c> if handler was successfully found and removed, <c>false</c> otherwise.</returns>
        public bool RemoveHandler(ushort packetId = ushort.MaxValue) =>
            _packetIdToHandler.Remove(packetId);
        
        /// <summary>
        /// Gets <see cref="PacketHandler"/> associated with given packet ID.
        /// </summary>
        /// <returns><c>true</c> if handler exists for given ID, <c>false</c> otherwise.</returns>
        public bool TryGetHandler(ushort packetId, out PacketHandler packetHandler) =>
            _packetIdToHandler.TryGetValue(packetId, out packetHandler);

        /// <summary>
        /// Attempts to get connection associated with the specified client end-point.
        /// </summary>
        /// <returns><c>true</c> if connection was found, <c>false</c> otherwise.</returns>
        public bool TryGetConnection(EndPoint clientEndPoint, out Connection? connection) =>
            _connections.TryGetValue(clientEndPoint, out connection);

        /// <summary>
        /// Stops this server which disconnects all the clients
        /// and prevents any further incoming packets.
        /// </summary>
        public void Stop()
        {
            Dispose();
            Stopped?.Invoke(new StoppedEventArgs(this));
        }

        protected override void Dispose(bool isDisposing)
        {
            foreach (var connection in _connections.Values)
                connection.Close();

            _connections.Clear();
            base.Dispose(isDisposing);
        }
        
        public abstract class EventArgs
        {
            /// <summary>Server that raised the event.</summary>
            public Server Server { get; }

            protected EventArgs(Server server) => Server = server;
        }

        public class StartedEventArgs : EventArgs
        {
            // Empty class that is reserved for potential future extending.
            internal StartedEventArgs(Server server) : base(server) { }
        }

        public class ClientConnectedEventArgs : EventArgs
        {
            /// <summary>Connection of the client that has connected.</summary>
            public Connection Connection { get; }
            
            internal ClientConnectedEventArgs(Server server, Connection connection) : base(server) => Connection = connection;
        }

        public class ClientDisconnectedEventArgs : EventArgs
        {
            /// <summary>Connection of the client that has disconnected.</summary>
            public Connection Connection { get; }
            
            /// <summary>Reason that the client has disconnected.</summary>
            public DisconnectCause Cause { get; }
            
            internal ClientDisconnectedEventArgs(Server server, Connection connection, DisconnectCause cause) : base(server)
            {
                Connection = connection;
                Cause = cause;
            }
        }
        
        public class StoppedEventArgs : EventArgs
        {
            // Empty class that is reserved for potential future extending.
            internal StoppedEventArgs(Server server) : base(server) { }
        }
    }
}
