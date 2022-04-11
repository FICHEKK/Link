using System;
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
        /// Returns current number of client connections.
        /// </summary>
        public int ConnectionCount => _connections.Count;

        /// <summary>
        /// Returns maximum allowed number of simultaneous client connections.
        /// </summary>
        public int MaxConnectionCount { get; private set; }

        /// <summary>
        /// Connections to all of the clients.
        /// </summary>
        private readonly Dictionary<EndPoint, Connection> _connections = new();

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

        protected override void Receive(ReadOnlySpan<byte> datagram, EndPoint senderEndPoint)
        {
            var headerType = (HeaderType) datagram[0];

            switch (headerType)
            {
                case HeaderType.Data:
                    TryGetConnection(senderEndPoint, headerType)?.ReceiveData(datagram);
                    return;
                
                case HeaderType.Acknowledgement:
                    TryGetConnection(senderEndPoint, headerType)?.ReceiveAcknowledgement(datagram);
                    return;

                case HeaderType.Ping:
                    TryGetConnection(senderEndPoint, headerType)?.ReceivePing(datagram);
                    return;

                case HeaderType.Pong:
                    TryGetConnection(senderEndPoint, headerType)?.ReceivePong(datagram);
                    return;

                case HeaderType.Connect:
                    HandleConnectPacket(senderEndPoint);
                    return;

                case HeaderType.Disconnect:
                    HandleDisconnectPacket(senderEndPoint);
                    return;

                default:
                    Log.Warning($"Server received invalid packet header {datagram[0]} from {senderEndPoint}.");
                    return;
            }
        }

        private void HandleConnectPacket(EndPoint senderEndPoint)
        {
            // Client is already connected, but might have not received the approval.
            if (_connections.TryGetValue(senderEndPoint, out var connection))
            {
                connection.ReceiveConnect();
                return;
            }

            // If server is full, ignore the sender.
            if (ConnectionCount >= MaxConnectionCount) return;

            // Else accept a new client connection.
            connection = new Connection(node: this, remoteEndPoint: senderEndPoint);
            connection.ReceiveConnect();

            _connections.Add(senderEndPoint, connection);
            EnqueuePendingAction(() => ClientConnected?.Invoke(connection));
        }

        private void HandleDisconnectPacket(EndPoint senderEndPoint)
        {
            var connection = TryGetConnection(senderEndPoint, HeaderType.Disconnect);
            if (connection is null) return;

            connection.Close(sendDisconnectPacket: false);
            _connections.Remove(senderEndPoint);
            EnqueuePendingAction(() => ClientDisconnected?.Invoke(connection));
        }

        private Connection TryGetConnection(EndPoint senderEndPoint, HeaderType headerType)
        {
            if (_connections.TryGetValue(senderEndPoint, out var connection)) return connection;

            Log.Warning($"Received '{headerType}' packet from a non-connected client at {senderEndPoint}.");
            return null;
        }

        internal override void Timeout(Connection connection)
        {
            if (!_connections.Remove(connection.RemoteEndPoint)) return;

            Log.Info($"Client from {connection.RemoteEndPoint} timed-out.");
            connection.Close(sendDisconnectPacket: false);
            EnqueuePendingAction(() => ClientDisconnected?.Invoke(connection));
        }

        /// <summary>
        /// Sends given packet to every connected client.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        public void Broadcast(Packet packet)
        {
            foreach (var connection in _connections.Values)
                connection.Send(packet);

            packet.Return();
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
