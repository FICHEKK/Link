using System;
using System.Net;
using System.Threading.Tasks;

namespace Link.Nodes
{
    /// <summary>
    /// Represents a network node that has one to one relationship with the server.
    /// Client never directly communicates with any other client, only with the server.
    /// </summary>
    public class Client : Node
    {
        /// <summary>
        /// Represents a method that creates connect packet by filling it with required data.
        /// </summary>
        public delegate void ConnectPacketFactory(Packet connectPacket);
        
        /// <summary>
        /// Invoked each time client starts the process of establishing connection with the server.
        /// </summary>
        public event Action? Connecting;
        
        /// <summary>
        /// Invoked each time client successfully connects to the server.
        /// </summary>
        public event Action? Connected;

        /// <summary>
        /// Invoked each time client fails to establish a connection with the server as maximum number
        /// of connect attempts was reached without getting a server response. This indicates that either
        /// server is offline or all of the packets were lost in transit (due to firewall, congestion or
        /// any other possible packet loss reason).
        /// </summary>
        public event Action? ConnectFailed;

        /// <summary>
        /// Invoked each time client disconnects from the server.
        /// </summary>
        public event Action<DisconnectCause>? Disconnected;

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
        public Connection? Connection { get; private set; }

        /// <summary>
        /// Attempts to establish a connection with the server.
        /// </summary>
        /// <param name="ipAddress">Server IP address.</param>
        /// <param name="port">Server port.</param>
        /// <param name="maxAttempts">Maximum number of connect attempts before considering server as unreachable.</param>
        /// <param name="delayBetweenAttempts">Delay between consecutive connect attempts, in milliseconds.</param>
        /// <param name="connectPacketFactory">Allows additional data to be written to the connect packet.</param>
        public void Connect(string ipAddress, int port, int maxAttempts = 5, int delayBetweenAttempts = 1000, ConnectPacketFactory? connectPacketFactory = null)
        {
            Listen(port: 0);
            Connection = new Connection(node: this, remoteEndPoint: new IPEndPoint(IPAddress.Parse(ipAddress), port), initialState: Connection.State.Connecting);
            ConnectionInitializer?.Invoke(Connection);
            
            Establish(maxAttempts, delayBetweenAttempts, connectPacketFactory);
            Connecting?.Invoke();
        }
        
        private async void Establish(int maxAttempts, int delayBetweenAttempts, ConnectPacketFactory? connectPacketFactory = null)
        {
            if (maxAttempts <= 0)
                throw new ArgumentException($"'{nameof(maxAttempts)}' must be a positive value.");
            
            if (delayBetweenAttempts <= 0)
                throw new ArgumentException($"'{nameof(delayBetweenAttempts)}' must be a positive value.");

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                SendConnectPacket();
                
                await Task.Delay(delayBetweenAttempts);
                if (!IsConnecting) return;
            }

            Dispose();
            ConnectFailed?.Invoke();

            void SendConnectPacket()
            {
                var connectPacket = Packet.Get(HeaderType.Connect);
                connectPacketFactory?.Invoke(connectPacket);
                Send(connectPacket, Connection!.RemoteEndPoint);
                connectPacket.Return();
            }
        }

        protected override void Consume(ReadOnlyPacket packet, EndPoint senderEndPoint)
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

            switch ((HeaderType) packet.Read<byte>())
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
                    Disconnect(DisconnectCause.ServerLogic);
                    return;
                
                case HeaderType.Timeout:
                    Disconnect(DisconnectCause.Timeout);
                    return;

                default:
                    Log.Warning("Client received invalid packet header from server.");
                    return;
            }
        }

        /// <summary>
        /// Sends a packet to the server.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        public void Send(Packet packet)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Cannot send packet as client is not connected to the server.");

            Connection!.SendData(packet);
            packet.Return();
        }

        /// <summary>
        /// Disconnects from the server and stops listening for incoming packets.
        /// </summary>
        public void Disconnect() => Disconnect(DisconnectCause.ClientLogic);

        private void Disconnect(DisconnectCause cause)
        {
            Dispose();
            Disconnected?.Invoke(cause);
        }

        protected override void Dispose(bool isDisposing)
        {
            Connection?.Close();
            Connection = null;
            base.Dispose(isDisposing);
        }
    }
}
