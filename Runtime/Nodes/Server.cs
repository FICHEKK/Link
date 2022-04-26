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
        public delegate bool ConnectPacketHandler(Packet connectPacket, EndPoint clientEndPoint);

        /// <summary>
        /// Validates incoming connection request and decides whether connection should be accepted or not.
        /// </summary>
        public ConnectPacketHandler ConnectionValidator { get; set; }

        /// <summary>
        /// Returns current number of client connections.
        /// </summary>
        public int ConnectionCount => _connections.Count;

        /// <summary>
        /// Returns maximum allowed number of simultaneous client connections.
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
        /// Starts this server and listens for incoming client connections.
        /// </summary>
        /// <param name="port">Port to listen on.</param>
        /// <param name="maxClientCount">Maximum number of clients allowed.</param>
        public void Start(int port, int maxClientCount)
        {
            MaxConnectionCount = maxClientCount;
            StartListening(port);
            Started?.Invoke();
        }

        protected override void Receive(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            switch ((HeaderType) datagram[0])
            {
                case HeaderType.Data:
                    TryGetConnection(senderEndPoint)?.ReceiveData(datagram, bytesReceived);
                    return;

                case HeaderType.Acknowledgement:
                    TryGetConnection(senderEndPoint)?.ReceiveAcknowledgement(datagram);
                    return;

                case HeaderType.Ping:
                    TryGetConnection(senderEndPoint)?.ReceivePing(datagram);
                    return;

                case HeaderType.Pong:
                    TryGetConnection(senderEndPoint)?.ReceivePong(datagram);
                    return;

                case HeaderType.Connect:
                    HandleConnectPacket(datagram, bytesReceived, senderEndPoint);
                    return;

                case HeaderType.Disconnect:
                    HandleDisconnectPacket(senderEndPoint);
                    return;

                default:
                    Log.Warning($"Server received invalid packet header {datagram[0]} from {senderEndPoint}.");
                    return;
            }
        }

        private void HandleConnectPacket(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            // Client is already connected, but might have not received the approval.
            if (_connections.TryGetValue(senderEndPoint, out var connection))
            {
                connection.ReceiveConnect();
                return;
            }

            if (ConnectionCount >= MaxConnectionCount)
            {
                Log.Info($"Client connection from {senderEndPoint} was declined as server is full.");
                return;
            }

            if (ConnectionValidator is not null && !ConnectionValidator(Packet.From(datagram, bytesReceived, readPosition: 1), senderEndPoint))
            {
                Log.Info($"Client connection from {senderEndPoint} was declined as it did not pass the validation test.");
                return;
            }

            Log.Info($"Client from {senderEndPoint} connected.");
            connection = new Connection(node: this, remoteEndPoint: senderEndPoint);
            ConnectionInitializer?.Invoke(connection);
            connection.ReceiveConnect();

            _connections.TryAdd(senderEndPoint, connection);
            EnqueuePendingAction(() => ClientConnected?.Invoke(connection));
        }

        private void HandleDisconnectPacket(EndPoint senderEndPoint) =>
            TryDisconnect(senderEndPoint, "disconnected");

        internal override void Timeout(Connection connection) =>
            TryDisconnect(connection.RemoteEndPoint, "timed-out");

        private void TryDisconnect(EndPoint clientEndPoint, string disconnectMethod)
        {
            if (!_connections.TryRemove(clientEndPoint, out var connection)) return;

            Log.Info($"Client from {clientEndPoint} {disconnectMethod}.");
            connection.Close(sendDisconnectPacket: false);
            EnqueuePendingAction(() => ClientDisconnected?.Invoke(connection));
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
        public void Stop()
        {
            foreach (var connection in _connections.Values)
                connection.Close(sendDisconnectPacket: true);

            StopListening();
            _connections.Clear();
            Stopped?.Invoke();
        }
    }
}
