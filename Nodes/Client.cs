using System;
using System.Net;
using Networking.Attributes;
using UnityEngine;

namespace Networking.Transport.Nodes
{
    /// <summary>
    /// Represents a network node that has one to one relationship with the server.
    /// Client never directly communicates with any other client, only with the server.
    /// </summary>
    public class Client : Node
    {
        /// <summary>
        /// Invoked each time client starts to process of establishing connection with the server.
        /// </summary>
        public event Action<EndPoint> OnConnectingToServer;

        /// <summary>
        /// Invoked each time client successfully connects to the server.
        /// </summary>
        public event Action<EndPoint> OnConnectedToServer;

        /// <summary>
        /// Invoked each time client fails to connect to the server for any reason.
        /// </summary>
        public event Action<string> OnCouldNotConnectToServer;

        /// <summary>
        /// Invoked each time client disconnects from the server.
        /// </summary>
        public event Action OnDisconnectedFromServer;
        
        /// <summary>
        /// Returns true if this client is currently connected to the server.
        /// </summary>
        public bool IsConnected { get; private set; }
        
        /// <summary>
        /// Returns server end-point if this client is connected to the server, null otherwise.
        /// </summary>
        public EndPoint ServerEndPoint { get; private set; }

        public Client()
        {
            RegisterPacketHandlersOfType<ClientPacketHandlerAttribute>();
            RegisterPacketHandler(PacketType.ConnectionAccepted.Id(), HandleConnectionAccepted);
            RegisterPacketHandler(PacketType.ConnectionDeclined.Id(), HandleConnectionDeclined);
            RegisterPacketHandler(PacketType.ConnectionClosing.Id(), HandleConnectionClosing);
        }

        private void HandleConnectionAccepted(PacketReader reader, EndPoint senderEndPoint)
        {
            EnsurePacketIsNotMalicious(senderEndPoint);
            IsConnected = true;
            OnConnectedToServer?.Invoke(ServerEndPoint);
        }

        private void HandleConnectionDeclined(PacketReader reader, EndPoint senderEndPoint)
        {
            EnsurePacketIsNotMalicious(senderEndPoint);
            ServerEndPoint = null;
            OnCouldNotConnectToServer?.Invoke(reader.ReadString());
        }

        private void HandleConnectionClosing(PacketReader reader, EndPoint senderEndPoint)
        {
            EnsurePacketIsNotMalicious(senderEndPoint);
            DisconnectInternal();
        }

        private void EnsurePacketIsNotMalicious(EndPoint senderEndPoint)
        {
            if (ServerEndPoint == null)
                throw new InvalidOperationException("Malicious packet: Connection request was not yet sent, so response from server could not have arrived.");

            if (!ServerEndPoint.Equals(senderEndPoint))
                throw new InvalidOperationException("Malicious packet: Packet end-point does not match server end-point.");
        }

        /// <summary>
        /// Attempts to establish a connection with the server.
        /// </summary>
        /// <param name="ipAddress">Server IP address.</param>
        /// <param name="port">Server port.</param>
        public void Connect(string ipAddress, int port)
        {
            if (IsConnected) throw new InvalidOperationException("Client is already connected.");

            StartListening(port: 0);
            ServerEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            Send(Packet.Get(PacketType.ConnectionRequest.Id()), ServerEndPoint);
            OnConnectingToServer?.Invoke(ServerEndPoint);
        }

        /// <summary>
        /// Sends a packet to the server.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        public void Send(Packet packet)
        {
            if (!IsConnected) throw new InvalidOperationException("Client is not connected to the server.");
            Send(packet, ServerEndPoint);
        }

        /// <summary>
        /// Disconnects from the server and stops listening for incoming packets.
        /// </summary>
        public void Disconnect()
        {
            if (IsConnected)
            {
                Send(Packet.Get(PacketType.ConnectionClosing.Id()));
                DisconnectInternal();
            }

            StopListening();
        }

        private void DisconnectInternal()
        {
            IsConnected = false;
            ServerEndPoint = null;
            OnDisconnectedFromServer?.Invoke();
        }
    }
}
