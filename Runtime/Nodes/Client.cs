using System;
using System.Net;

namespace Link.Nodes
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
        public event Action Connecting;

        /// <summary>
        /// Invoked each time client successfully connects to the server.
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// Invoked each time client disconnects from the server.
        /// </summary>
        public event Action Disconnected;

        /// <summary>
        /// Returns <c>true</c> if this client is currently attempting to connect to the server.
        /// </summary>
        public bool IsConnecting => Connection is not null && Connection.CurrentState == Connection.State.Connecting;

        /// <summary>
        /// Returns <c>true</c> if this client is currently connected to the server.
        /// </summary>
        public bool IsConnected => Connection is not null && Connection.CurrentState == Connection.State.Connected;

        /// <summary>
        /// Connection to the server.
        /// </summary>
        public Connection Connection { get; private set; }

        /// <summary>
        /// Attempts to establish a connection with the server.
        /// </summary>
        /// <param name="ipAddress">Server IP address.</param>
        /// <param name="port">Server port.</param>
        /// <param name="maxAttempts">Maximum number of connect attempts before considering server as unreachable.</param>
        /// <param name="delayBetweenAttempts">Delay between consecutive connect attempts, in milliseconds.</param>
        /// <param name="connectPacketWriter">Allows additional data to be written to the connect packet.</param>
        public void Connect(string ipAddress, int port, int maxAttempts = 5, int delayBetweenAttempts = 1000, Action<Packet> connectPacketWriter = null)
        {
            Listen(port: 0);
            Connection = new Connection(node: this, remoteEndPoint: new IPEndPoint(IPAddress.Parse(ipAddress), port));
            ConnectionInitializer?.Invoke(Connection);
            Connection.Establish(maxAttempts, delayBetweenAttempts, connectPacketWriter);
            Connecting?.Invoke();
        }

        protected override void Consume(Packet packet, EndPoint senderEndPoint)
        {
            if (Connection is null)
            {
                Log.Warning("Cannot receive on client with no server connection.");
                return;
            }

            if (!Connection.RemoteEndPoint.Equals(senderEndPoint))
            {
                Log.Warning("Malicious packet: Packet end-point does not match server end-point.");
                return;
            }

            switch (packet.HeaderType)
            {
                case HeaderType.Data:
                    Connection.ReceiveData(packet);
                    return;

                case HeaderType.Acknowledgement:
                    Connection.ReceiveAcknowledgement(packet);
                    return;

                case HeaderType.Ping:
                    Connection.ReceivePing(packet);
                    return;

                case HeaderType.Pong:
                    Connection.ReceivePong(packet);
                    return;

                case HeaderType.ConnectApproved:
                    Connection.ReceiveConnectApproved();
                    Connected?.Invoke();
                    return;

                case HeaderType.Disconnect:
                    Disconnect();
                    return;

                default:
                    Log.Warning($"Client received invalid packet header {packet.HeaderType} from server.");
                    return;
            }
        }

        /// <summary>
        /// Sends a packet to the server.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        public void Send(Packet packet)
        {
            if (!IsConnected) throw new InvalidOperationException("Cannot send packet as client is not connected to the server.");

            Connection.SendData(packet);
            packet.Return();
        }

        /// <summary>
        /// Disconnects from the server and stops listening for incoming packets.
        /// </summary>
        public void Disconnect() => Dispose();
        
        internal override void Timeout(Connection connection) => Dispose();

        protected override void Dispose(bool isDisposing)
        {
            if (Connection is not null)
            {
                Connection.Dispose();
                Connection = null;
                Disconnected?.Invoke();
            }

            base.Dispose(isDisposing);
        }
    }
}
