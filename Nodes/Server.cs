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
        public event Action<int> OnStarted;

        /// <summary>
        /// Invoked each time a new client connects to the server.
        /// </summary>
        public event Action<EndPoint> OnClientConnected;

        /// <summary>
        /// Invoked each time an already connected client disconnects from the server.
        /// </summary>
        public event Action<EndPoint> OnClientDisconnected;

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
        private readonly Dictionary<EndPoint, Connection> _connections = new Dictionary<EndPoint, Connection>();

        /// <summary>
        /// Starts this server and listens for incoming client connections.
        /// </summary>
        /// <param name="port">Port to listen on.</param>
        /// <param name="maxClientCount">Maximum number of clients allowed.</param>
        public void Start(int port, int maxClientCount)
        {
            if (IsListening) throw new InvalidOperationException("Server has already started.");

            StartListening(port);
            MaxConnectionCount = maxClientCount;
            OnStarted?.Invoke(port);
        }

        protected override Packet Receive(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            switch ((HeaderType) datagram[0])
            {
                case HeaderType.Connect:
                    HandleConnectPacket(senderEndPoint);
                    return null;

                case HeaderType.UnreliableData:
                case HeaderType.SequencedData:
                case HeaderType.ReliableData:
                    return HandleDataPacket(datagram, bytesReceived, senderEndPoint);

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
                _connections.Add(senderEndPoint, new Connection {RemoteEndPoint = senderEndPoint, CurrentState = Connection.State.Connected});
                Send(Packet.Get(HeaderType.ConnectApproved), senderEndPoint);
                ExecuteOnMainThread(() => OnClientConnected?.Invoke(senderEndPoint));
            }
        }

        private Packet HandleDataPacket(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            if (!_connections.TryGetValue(senderEndPoint, out var connection))
            {
                Log.Warning($"Received data packet from a non-connected client at {senderEndPoint}.");
                return null;
            }

            return connection.PreparePacketForHandling(datagram, bytesReceived);
        }

        private void HandleDisconnectPacket(EndPoint senderEndPoint)
        {
            if (!_connections.ContainsKey(senderEndPoint))
            {
                Log.Warning($"Could not disconnect client from {senderEndPoint} as client was not connected.");
                return;
            }

            Log.Info($"Client from {senderEndPoint} requested disconnect...");
            _connections.Remove(senderEndPoint);
            ExecuteOnMainThread(() => OnClientDisconnected?.Invoke(senderEndPoint));
        }

        /// <summary>
        /// Sends given packet to every connected client.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        public void Broadcast(Packet packet)
        {
            foreach (var connection in _connections.Values)
            {
                connection.PreparePacketForSending(packet);
                SendWithoutReturningToPool(packet, connection.RemoteEndPoint);
            }

            packet.Return();
        }

        /// <summary>
        /// Stops this server which disconnects all the clients
        /// and prevents any further incoming packets.
        /// </summary>
        public void Stop()
        {
            Broadcast(Packet.Get(HeaderType.Disconnect));
            StopListening();
            _connections.Clear();
            OnStopped?.Invoke();
        }
    }
}
