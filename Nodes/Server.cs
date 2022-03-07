using System;
using System.Collections.Generic;
using System.Net;
using Networking.Exceptions;

namespace Networking.Transport.Nodes
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
        public event Action OnStarted;

        /// <summary>
        /// Invoked each time a new client connects to the server.
        /// </summary>
        public event Action<Connection> OnClientConnected;

        /// <summary>
        /// Invoked each time an already connected client disconnects from the server.
        /// </summary>
        public event Action<Connection> OnClientDisconnected;

        /// <summary>
        /// Invoked each time server stops and no longer listens for client connections.
        /// </summary>
        public event Action OnStopped;

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
            if (IsListening) throw Error.ServerAlreadyStarted("Cannot start a server that is already running.");

            StartListening(port);
            MaxConnectionCount = maxClientCount;
            OnStarted?.Invoke();
        }

        protected override Packet Receive(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            switch ((HeaderType) datagram[0])
            {
                case HeaderType.Connect:
                    HandleConnectPacket(senderEndPoint);
                    return null;

                case HeaderType.UnreliableData or HeaderType.SequencedData or HeaderType.ReliableData:
                    return HandleDataPacket(datagram, bytesReceived, senderEndPoint);

                case HeaderType.Ping:
                    HandlePingPacket(datagram, senderEndPoint);
                    return null;

                case HeaderType.Pong:
                    HandlePongPacket(datagram, senderEndPoint);
                    return null;

                case HeaderType.Disconnect:
                    HandleDisconnectPacket(senderEndPoint);
                    return null;

                default:
                    Log.Warning($"Server received invalid packet header {datagram[0]:D} from {senderEndPoint}.");
                    return null;
            }
        }

        private void HandleConnectPacket(EndPoint senderEndPoint)
        {
            Log.Info($"Client from {senderEndPoint} is trying to connect...");

            // This client is already connected, but might have not received the approval.
            if (_connections.ContainsKey(senderEndPoint))
            {
                Send(Packet.Get(HeaderType.ConnectApproved), senderEndPoint);
                return;
            }

            // If server is not full, we accept new connection. Otherwise, ignore the sender.
            if (ConnectionCount < MaxConnectionCount)
            {
                var connection = new Connection(node: this, remoteEndPoint: senderEndPoint, isConnected: true);
                _connections.Add(senderEndPoint, connection);
                ExecuteOnMainThread(() => OnClientConnected?.Invoke(connection));
            }
        }

        private Packet HandleDataPacket(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            if (_connections.TryGetValue(senderEndPoint, out var connection))
                return connection.Receive(datagram, bytesReceived);

            Log.Warning($"Received data packet from a non-connected client at {senderEndPoint}.");
            return null;
        }

        private void HandlePingPacket(byte[] datagram, EndPoint senderEndPoint)
        {
            if (_connections.TryGetValue(senderEndPoint, out var connection))
            {
                connection.ReceivePing(datagram);
            }
            else
            {
                Log.Warning($"Received ping packet from a non-connected client at {senderEndPoint}.");
            }
        }

        private void HandlePongPacket(byte[] datagram, EndPoint senderEndPoint)
        {
            if (_connections.TryGetValue(senderEndPoint, out var connection))
            {
                connection.ReceivePong(datagram);
            }
            else
            {
                Log.Warning($"Received pong packet from a non-connected client at {senderEndPoint}.");
            }
        }

        private void HandleDisconnectPacket(EndPoint senderEndPoint)
        {
            if (!_connections.ContainsKey(senderEndPoint))
            {
                Log.Warning($"Could not disconnect client from {senderEndPoint} as client was not connected.");
                return;
            }

            Log.Info($"Client from {senderEndPoint} requested disconnect...");

            var connection = _connections[senderEndPoint];
            connection.Close(sendDisconnectPacket: false);
            _connections.Remove(senderEndPoint);
            ExecuteOnMainThread(() => OnClientDisconnected?.Invoke(connection));
        }

        /// <summary>
        /// Sends given packet to every connected client.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        public void Broadcast(Packet packet)
        {
            foreach (var connection in _connections.Values)
                connection.Send(packet, returnPacketToPool: false);

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
            OnStopped?.Invoke();
        }
    }
}
