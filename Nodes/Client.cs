using System;
using System.Net;
using Networking.Exceptions;

namespace Networking.Transport.Nodes
{
    /// <summary>
    /// Represents a network node that has one to one relationship with the server.
    /// Client never directly communicates with any other client, only with the server.
    /// </summary>
    public class Client : Node
    {
        /// <summary>
        /// Invoked each time client starts the process of establishing connection with the server.
        /// </summary>
        public event Action OnConnectingToServer;

        /// <summary>
        /// Invoked each time client successfully connects to the server.
        /// </summary>
        public event Action OnConnectedToServer;

        /// <summary>
        /// Invoked each time client disconnects from the server.
        /// </summary>
        public event Action OnDisconnectedFromServer;

        /// <summary>
        /// Returns <c>true</c> if this client is currently attempting to connect to the server.
        /// </summary>
        public bool IsConnecting => Connection is not null && !Connection.IsConnected;

        /// <summary>
        /// Returns <c>true</c> if this client is currently connected to the server.
        /// </summary>
        public bool IsConnected => Connection is not null && Connection.IsConnected;

        /// <summary>
        /// Connection to the server.
        /// </summary>
        public Connection Connection { get; private set; }

        /// <summary>
        /// Attempts to establish a connection with the server.
        /// </summary>
        /// <param name="ipAddress">Server IP address.</param>
        /// <param name="port">Server port.</param>
        public void Connect(string ipAddress, int port)
        {
            if (IsConnecting) throw Error.ClientAlreadyConnecting("Client is currently trying to connect.");
            if (IsConnected) throw Error.ClientAlreadyConnected("Client is already connected.");

            StartListening(port: 0);
            Connection = new Connection(node: this, remoteEndPoint: new IPEndPoint(IPAddress.Parse(ipAddress), port), isConnected: false);
            OnConnectingToServer?.Invoke();
        }

        protected override Packet Receive(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            if (Connection is null || !Connection.RemoteEndPoint.Equals(senderEndPoint))
            {
                Log.Warning("Malicious packet: Packet end-point does not match server end-point.");
                return null;
            }

            switch ((HeaderType) datagram[0])
            {
                case HeaderType.ConnectApproved:
                    Connection.IsConnected = true;
                    ExecuteOnMainThread(() => OnConnectedToServer?.Invoke());
                    return null;

                case HeaderType.UnreliableData or HeaderType.SequencedData or HeaderType.ReliableData:
                    return Connection.Receive(datagram, bytesReceived);

                case HeaderType.Ping:
                    Connection.ReceivePing(datagram);
                    return null;

                case HeaderType.Pong:
                    Connection.ReceivePong(datagram);
                    return null;

                case HeaderType.Disconnect:
                    Connection.Close(sendDisconnectPacket: false);
                    Connection = null;
                    ExecuteOnMainThread(() => OnDisconnectedFromServer?.Invoke());
                    return null;

                default:
                    Log.Warning($"Client received invalid packet header {datagram[0]:D} from server.");
                    return null;
            }
        }

        /// <summary>
        /// Sends a packet to the server.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        public void Send(Packet packet)
        {
            if (!IsConnected) throw Error.SendCalledOnDisconnectedClient("Client is not connected to the server.");
            Connection.Send(packet);
        }

        /// <summary>
        /// Disconnects from the server and stops listening for incoming packets.
        /// </summary>
        public void Disconnect()
        {
            if (Connection is not null)
            {
                Connection.Close(sendDisconnectPacket: true);
                Connection = null;
                OnDisconnectedFromServer?.Invoke();
            }

            StopListening();
        }
    }
}
