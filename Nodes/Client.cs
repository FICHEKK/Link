using System;
using System.Net;
using Networking.Attributes;

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
        /// Returns true if this client is currently attempting to connect to the server.
        /// </summary>
        public bool IsConnecting => _connection.CurrentState == Connection.State.Connecting;

        /// <summary>
        /// Returns true if this client is currently connected to the server.
        /// </summary>
        public bool IsConnected => _connection.CurrentState == Connection.State.Connected;

        /// <summary>
        /// Connection to the server.
        /// </summary>
        private readonly Connection _connection = new Connection();

        /// <summary>
        /// Constructs a new client instance, but does not attempt to connect.
        /// </summary>
        public Client() => RegisterPacketHandlersOfType<ClientPacketHandlerAttribute>();

        /// <summary>
        /// Attempts to establish a connection with the server.
        /// </summary>
        /// <param name="ipAddress">Server IP address.</param>
        /// <param name="port">Server port.</param>
        public void Connect(string ipAddress, int port)
        {
            if (IsConnecting) throw new InvalidOperationException("Client is currently trying to connect.");
            if (IsConnected) throw new InvalidOperationException("Client is already connected.");

            StartListening(port: 0);
            _connection.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            _connection.CurrentState = Connection.State.Connecting;
            Send(Packet.Get(HeaderType.Connect), _connection.RemoteEndPoint);

            OnConnectingToServer?.Invoke(_connection.RemoteEndPoint);
        }

        protected override Packet Receive(byte[] datagram, int bytesReceived, EndPoint senderEndPoint)
        {
            if (!_connection.RemoteEndPoint.Equals(senderEndPoint))
            {
                Log.Warning("Malicious packet: Packet end-point does not match server end-point.");
                return null;
            }

            switch ((HeaderType) datagram[0])
            {
                case HeaderType.ConnectApproved:
                    HandleConnectApprovedPacket();
                    return null;

                case HeaderType.UnreliableData:
                case HeaderType.SequencedData:
                case HeaderType.ReliableData:
                    return HandleDataPacket(datagram, bytesReceived);

                case HeaderType.Disconnect:
                    HandleDisconnectPacket();
                    return null;

                default:
                    Log.Warning($"Client received invalid packet header {datagram[0]:D} from server.");
                    return null;
            }
        }

        private void HandleConnectApprovedPacket()
        {
            _connection.CurrentState = Connection.State.Connected;
            ExecuteOnMainThread(() => OnConnectedToServer?.Invoke(_connection.RemoteEndPoint));
        }

        private Packet HandleDataPacket(byte[] datagram, int bytesReceived) =>
            _connection.PreparePacketForHandling(datagram, bytesReceived);

        private void HandleDisconnectPacket()
        {
            _connection.RemoteEndPoint = null;
            _connection.CurrentState = Connection.State.Disconnected;
            ExecuteOnMainThread(() => OnDisconnectedFromServer?.Invoke());
        }

        /// <summary>
        /// Sends a packet to the server.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        public void Send(Packet packet)
        {
            if (!IsConnected) throw new InvalidOperationException("Client is not connected to the server.");
            _connection.PreparePacketForSending(packet);
            Send(packet, _connection.RemoteEndPoint);
        }

        /// <summary>
        /// Disconnects from the server and stops listening for incoming packets.
        /// </summary>
        public void Disconnect()
        {
            if (IsConnected)
            {
                Send(Packet.Get(HeaderType.Disconnect), _connection.RemoteEndPoint);
                _connection.RemoteEndPoint = null;
                _connection.CurrentState = Connection.State.Disconnected;
                OnDisconnectedFromServer?.Invoke();
            }

            StopListening();
        }
    }
}
