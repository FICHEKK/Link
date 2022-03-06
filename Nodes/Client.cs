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
        public event Action<Connection> OnConnectingToServer;

        /// <summary>
        /// Invoked each time client successfully connects to the server.
        /// </summary>
        public event Action<Connection> OnConnectedToServer;

        /// <summary>
        /// Invoked each time client fails to connect to the server for any reason.
        /// </summary>
        public event Action<string> OnCouldNotConnectToServer;

        /// <summary>
        /// Invoked each time client disconnects from the server.
        /// </summary>
        public event Action OnDisconnectedFromServer;

        /// <summary>
        /// Returns <c>true</c> if this client is currently attempting to connect to the server.
        /// </summary>
        public bool IsConnecting => _connection is not null && !_connection.IsConnected;

        /// <summary>
        /// Returns <c>true</c> if this client is currently connected to the server.
        /// </summary>
        public bool IsConnected => _connection is not null && _connection.IsConnected;

        /// <summary>
        /// Connection to the server.
        /// </summary>
        private Connection _connection;

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
            _connection = new Connection(node: this, remoteEndPoint: new IPEndPoint(IPAddress.Parse(ipAddress), port), isConnected: false);
            OnConnectingToServer?.Invoke(_connection);
        }

        protected override Packet Receive(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            if (_connection is null || !_connection.RemoteEndPoint.Equals(senderEndPoint))
            {
                Log.Warning("Malicious packet: Packet end-point does not match server end-point.");
                return null;
            }

            switch ((HeaderType) datagram[0])
            {
                case HeaderType.ConnectApproved:
                    _connection.IsConnected = true;
                    ExecuteOnMainThread(() => OnConnectedToServer?.Invoke(_connection));
                    return null;

                case HeaderType.UnreliableData or HeaderType.SequencedData or HeaderType.ReliableData:
                    return _connection.Receive(datagram, bytesReceived);

                case HeaderType.Ping:
                    _connection.ReceivePing(datagram);
                    return null;

                case HeaderType.Pong:
                    _connection.ReceivePong(datagram);
                    return null;

                case HeaderType.Disconnect:
                    _connection = null;
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
            _connection.Send(packet);
        }

        /// <summary>
        /// Disconnects from the server and stops listening for incoming packets.
        /// </summary>
        public void Disconnect()
        {
            if (_connection is not null)
            {
                _connection.Close();
                _connection = null;
                OnDisconnectedFromServer?.Invoke();
            }

            StopListening();
        }
    }
}
