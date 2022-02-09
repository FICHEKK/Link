using System;
using System.Collections.Generic;
using System.Net;
using Networking.Attributes;
using UnityEngine;

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

        private readonly HashSet<EndPoint> _connections = new HashSet<EndPoint>();

        public Server()
        {
            RegisterPacketHandlersOfType<ServerPacketHandlerAttribute>();
            RegisterPacketHandler(PacketType.ConnectionRequest.Id(), HandleConnectionRequest);
            RegisterPacketHandler(PacketType.ConnectionClosing.Id(), HandleConnectionClosing);
        }

        private void HandleConnectionRequest(PacketReader reader, EndPoint senderEndPoint)
        {
            Debug.Log($"Client from {senderEndPoint} is trying to connect...");

            if (ConnectionCount >= MaxConnectionCount)
            {
                // TODO - Instead of sending strings, send special packet type instead.
                var connectionDeclinedPacket = Packet.Get(PacketType.ConnectionDeclined.Id());
                connectionDeclinedPacket.Writer.Write("Server is full.");
                Send(connectionDeclinedPacket, senderEndPoint);
                return;
            }

            if (_connections.Contains(senderEndPoint))
            {
                var connectionDeclinedPacket = Packet.Get(PacketType.ConnectionDeclined.Id());
                connectionDeclinedPacket.Writer.Write("You are already connected.");
                Send(connectionDeclinedPacket, senderEndPoint);
                return;
            }

            _connections.Add(senderEndPoint);

            Send(Packet.Get(PacketType.ConnectionAccepted.Id()), senderEndPoint);
            OnClientConnected?.Invoke(senderEndPoint);
        }

        private void HandleConnectionClosing(PacketReader reader, EndPoint senderEndPoint)
        {
            if (!_connections.Contains(senderEndPoint))
            {
                Debug.LogWarning($"Could not disconnect client from {senderEndPoint} as client was not connected.");
                return;
            }

            Debug.Log($"Client from {senderEndPoint} requested disconnect...");
            _connections.Remove(senderEndPoint);
            OnClientDisconnected?.Invoke(senderEndPoint);
        }

        /// <summary>
        /// Starts this server and listens for incoming client connections.
        /// </summary>
        /// <param name="port">Port to listen on.</param>
        /// <param name="maxClientCount">Maximum number of clients allowed.</param>
        public void Start(int port, int maxClientCount)
        {
            StartListening(port);
            MaxConnectionCount = maxClientCount;
            OnStarted?.Invoke(port);
        }

        /// <summary>
        /// Sends given packet to every connected client.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        public void Broadcast(Packet packet)
        {
            foreach (var connection in _connections)
            {
                SendWithoutReturningToPool(packet, connection);
            }

            packet.Return();
        }

        /// <summary>
        /// Stops this server which disconnects all the clients
        /// and prevents any further incoming packets.
        /// </summary>
        public void Stop()
        {
            Broadcast(Packet.Get(PacketType.ConnectionClosing.Id()));
            StopListening();
            _connections.Clear();
            OnStopped?.Invoke();
        }
    }
}
