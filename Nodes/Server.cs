using System;
using System.Collections.Generic;
using System.Net;

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
            StartListening(port);
            MaxConnectionCount = maxClientCount;
            OnStarted?.Invoke();
        }

        protected override void Receive(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            switch ((HeaderType) datagram[0])
            {
                case HeaderType.Data:
                    HandleDataPacket(datagram, bytesReceived, senderEndPoint);
                    return;
                
                case HeaderType.Acknowledgement:
                    HandleAcknowledgementPacket(datagram, senderEndPoint);
                    return;

                case HeaderType.Ping:
                    HandlePingPacket(datagram, senderEndPoint);
                    return;

                case HeaderType.Pong:
                    HandlePongPacket(datagram, senderEndPoint);
                    return;

                case HeaderType.Connect:
                    HandleConnectPacket(senderEndPoint);
                    return;

                case HeaderType.Disconnect:
                    HandleDisconnectPacket(senderEndPoint);
                    return;

                default:
                    Log.Warning($"Server received invalid packet header {datagram[0]:D} from {senderEndPoint}.");
                    return;
            }
        }

        internal override void Timeout(Connection connection)
        {
            var clientEndPoint = connection.RemoteEndPoint;
            if (!_connections.Remove(clientEndPoint)) return;

            Log.Info($"Client from {clientEndPoint} timed-out.");

            connection.Close(sendDisconnectPacket: false);
            ExecuteOnMainThread(() => OnClientDisconnected?.Invoke(connection));
        }

        private void HandleConnectPacket(EndPoint senderEndPoint)
        {
            Log.Info($"Client from {senderEndPoint} is trying to connect...");

            // This client is already connected, but might have not received the approval.
            if (_connections.ContainsKey(senderEndPoint))
            {
                var approvalPacket = Packet.Get(HeaderType.ConnectApproved);
                Send(approvalPacket, senderEndPoint);
                approvalPacket.Return();
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

        private void HandleDataPacket(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            if (_connections.TryGetValue(senderEndPoint, out var connection))
            {
                connection.ReceiveData(datagram, bytesReceived);
                return;
            }

            Log.Warning($"Received data packet from a non-connected client at {senderEndPoint}.");
        }

        private void HandleAcknowledgementPacket(byte[] datagram, EndPoint senderEndPoint)
        {
            if (_connections.TryGetValue(senderEndPoint, out var connection))
            {
                connection.ReceiveAcknowledgement(datagram);
                return;
            }

            Log.Warning($"Received acknowledgement packet from a non-connected client at {senderEndPoint}.");
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
            OnStopped?.Invoke();
        }
    }
}
