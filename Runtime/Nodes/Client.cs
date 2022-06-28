using System;
using System.Collections.Generic;
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
        /// Defines a method that handles incoming data-packet from the server.
        /// </summary>
        public delegate void PacketHandler(ReceiveArgs args);
        
        /// <summary>
        /// Represents a method that creates connect packet by filling it with required data.
        /// </summary>
        public delegate void ConnectPacketFactory(Packet connectPacket);
        
        /// <summary>
        /// Invoked each time client starts the process of establishing connection with the server.
        /// </summary>
        public event EventHandler<ConnectingEventArgs>? Connecting;
        
        /// <summary>
        /// Invoked each time client successfully connects to the server.
        /// </summary>
        public event EventHandler<ConnectedEventArgs>? Connected;

        /// <summary>
        /// Invoked each time client fails to establish a connection with the server as maximum number
        /// of connect attempts was reached without getting a server response. This indicates that either
        /// server is offline or all of the packets were lost in transit (due to firewall, congestion or
        /// any other possible packet loss reason).
        /// </summary>
        public event EventHandler<ConnectFailedEventArgs>? ConnectFailed;

        /// <summary>
        /// Invoked each time client disconnects from the server.
        /// </summary>
        public event EventHandler<DisconnectedEventArgs>? Disconnected;

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
        /// Maps packet IDs to their handlers.
        /// </summary>
        private readonly Dictionary<ushort, PacketHandler> _packetIdToHandler = new();

        /// <summary>
        /// Attempts to establish a connection with the server.
        /// </summary>
        /// <param name="ipAddress">Server IP address.</param>
        /// <param name="port">Server port.</param>
        /// <param name="maxAttempts">Maximum number of connect attempts before considering server as unreachable.</param>
        /// <param name="delayBetweenAttempts">Delay between consecutive connect attempts, in milliseconds.</param>
        /// <param name="connectPacketFactory">Allows additional data to be written to the connect packet.</param>
        public void Connect(string ipAddress, ushort port, int maxAttempts = 5, int delayBetweenAttempts = 1000, ConnectPacketFactory? connectPacketFactory = null)
        {
            Listen(port: 0);
            Connection = new Connection(node: this, remoteEndPoint: new IPEndPoint(IPAddress.Parse(ipAddress), port), initialState: Connection.State.Connecting);
            ConnectionInitializer?.Invoke(Connection);
            
            Establish(maxAttempts, delayBetweenAttempts, connectPacketFactory);
            Connecting?.Invoke(new ConnectingEventArgs(this));
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
            ConnectFailed?.Invoke(new ConnectFailedEventArgs(this));

            void SendConnectPacket()
            {
                var connectPacket = Packet.Get(HeaderType.Connect);
                connectPacketFactory?.Invoke(connectPacket);
                
                // Write padding to the packet before sending. This is a measure to prevent amplification attacks. 
                Send(connectPacket.WriteArray(new byte[connectPacket.UnwrittenBytes], writeLength: false), Connection!.RemoteEndPoint);
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

            var packetHeader = packet.Read<HeaderType>();

            switch (packetHeader)
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
                    Connected?.Invoke(new ConnectedEventArgs(this));
                    return;

                case HeaderType.Disconnect:
                    Disconnect(DisconnectCause.ServerLogic);
                    return;
                
                case HeaderType.Timeout:
                    Disconnect(DisconnectCause.Timeout);
                    return;

                default:
                    Log.Warning($"Client received invalid header byte of value {packetHeader} from server.");
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

        internal override void Receive(Buffer buffer, EndPoint senderEndPoint)
        {
            var packetId = (ushort) buffer.ReadShort(offset: 2);

            if (!_packetIdToHandler.TryGetValue(packetId, out var packetHandler))
            {
                Log.Warning($"{nameof(Client)} could not handle packet (ID = {packetId}) as there is no handler for it.");
                return;
            }

            packetHandler(new ReceiveArgs(this, new ReadOnlyPacket(buffer, start: Packet.HeaderSize)));
        }

        /// <summary>
        /// Adds a packet handler for a specific packet ID.
        /// </summary>
        /// <exception cref="InvalidOperationException">If given packet ID already has a handler.</exception>
        public void AddHandler(PacketHandler packetHandler, ushort packetId = ushort.MaxValue)
        {
            if (_packetIdToHandler.ContainsKey(packetId))
                throw new InvalidOperationException($"{nameof(Client)} already has a handler for packet ID {packetId}.");
            
            _packetIdToHandler.Add(packetId, packetHandler);
        }
        
        /// <summary>
        /// Removes a packet handler that is associated with a specific packet ID.
        /// </summary>
        /// <returns><c>true</c> if handler was successfully found and removed, <c>false</c> otherwise.</returns>
        public bool RemoveHandler(ushort packetId = ushort.MaxValue) =>
            _packetIdToHandler.Remove(packetId);
        
        /// <summary>
        /// Gets <see cref="PacketHandler"/> associated with given packet ID.
        /// </summary>
        /// <returns><c>true</c> if handler exists for given ID, <c>false</c> otherwise.</returns>
        public bool TryGetHandler(ushort packetId, out PacketHandler packetHandler) =>
            _packetIdToHandler.TryGetValue(packetId, out packetHandler);

        /// <summary>
        /// Disconnects from the server and stops listening for incoming packets.
        /// </summary>
        public void Disconnect() => Disconnect(DisconnectCause.ClientLogic);

        private void Disconnect(DisconnectCause cause)
        {
            Dispose();
            Disconnected?.Invoke(new DisconnectedEventArgs(this, cause));
        }

        protected override void Dispose(bool isDisposing)
        {
            Connection?.Close();
            Connection = null;
            base.Dispose(isDisposing);
        }
        
        /// <summary>
        /// Data associated with the packet received from the server. 
        /// </summary>
        public readonly ref struct ReceiveArgs
        {
            /// <summary>Client that has received the packet.</summary>
            public Client Client { get; }
            
            /// <summary>Packet that was received.</summary>
            public ReadOnlyPacket Packet { get; }

            internal ReceiveArgs(Client client, ReadOnlyPacket packet)
            {
                Client = client;
                Packet = packet;
            }
        }

        public abstract class EventArgs
        {
            /// <summary>Client that raised the event.</summary>
            public Client Client { get; }
            
            protected EventArgs(Client client) => Client = client;
        }

        public class ConnectingEventArgs : EventArgs
        {
            // Empty class that is reserved for potential future extending.
            internal ConnectingEventArgs(Client client) : base(client) { }
        }

        public class ConnectedEventArgs : EventArgs
        {
            // Empty class that is reserved for potential future extending.
            internal ConnectedEventArgs(Client client) : base(client) { }
        }

        public class ConnectFailedEventArgs : EventArgs
        {
            // Empty class that is reserved for potential future extending.
            internal ConnectFailedEventArgs(Client client) : base(client) { }
        }

        public class DisconnectedEventArgs : EventArgs
        {
            /// <summary>Reason that the client has disconnected.</summary>
            public DisconnectCause Cause { get; }
            
            internal DisconnectedEventArgs(Client client, DisconnectCause cause) : base(client) => Cause = cause;
        }
    }
}
