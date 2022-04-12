using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Link.Nodes
{
    /// <summary>
    /// Represents a network node - a fundamental building block of any network graph that can send and receive data.
    /// </summary>
    public abstract class Node
    {
        /// <summary>
        /// Default socket send and receive buffer size.
        /// </summary>
        private const int DefaultBufferSize = 1024 * 1024;

        /// <summary>
        /// Cached end-point instance used for <see cref="Socket.ReceiveFrom(byte[],ref System.Net.EndPoint)"/> calls.
        /// </summary>
        private static readonly EndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        /// <summary>
        /// Random instance used for network simulation purposes.
        /// </summary>
        private static readonly Random Random = new();

        /// <summary>
        /// Returns packet handlers registered for this network node.
        /// </summary>
        public IReadOnlyDictionary<ushort, Action<PacketReader, EndPoint>> PacketIdToPacketHandler => _packetIdToPacketHandler;

        /// <summary>
        /// Returns port on which this node is listening on, or <c>-1</c> if not currently listening.
        /// </summary>
        public int Port => IsListening ? ((IPEndPoint) _socket.LocalEndPoint).Port : -1;

        /// <summary>
        /// Returns true if this node is currently listening for incoming packets.
        /// </summary>
        public bool IsListening => _socket is not null;

        /// <summary>
        /// Defines the size of socket's internal send buffer.
        /// </summary>
        /// <remarks>Value must be non-negative, otherwise exception will be thrown on socket set-up.</remarks>
        public int SendBufferSize { get; set; } = DefaultBufferSize;

        /// <summary>
        /// Defines the size of socket's internal receive buffer.
        /// </summary>
        /// <remarks>Value must be non-negative, otherwise exception will be thrown on socket set-up.</remarks>
        public int ReceiveBufferSize { get; set; } = DefaultBufferSize;

        /// <summary>
        /// Defines the probability of a packet being lost.
        /// This property should only be used only for testing purposes.
        /// </summary>
        /// <remarks>Value must be in range from 0 to 1.</remarks>
        public float PacketLoss
        {
            get => _packetLoss;
            set => _packetLoss = value is >= 0 and <= 1 ? value : throw new ArgumentOutOfRangeException(nameof(PacketLoss));
        }

        /// <summary>
        /// Minimum additional delay (in ms) before processing received packet.
        /// </summary>
        /// <remarks>Value must be non-negative and less or equal to <see cref="MaxLatency"/>.</remarks>
        public int MinLatency
        {
            get => _minLatency;
            set => _minLatency = value >= 0 && value <= _maxLatency ? value : throw new ArgumentOutOfRangeException(nameof(MinLatency));
        }

        /// <summary>
        /// Maximum additional delay (in ms) before processing received packet.
        /// </summary>
        /// <remarks>Value must be non-negative and greater or equal to <see cref="MinLatency"/>.</remarks>
        public int MaxLatency
        {
            get => _maxLatency;
            set => _maxLatency = value >= 0 && value >= _minLatency ? value : throw new ArgumentOutOfRangeException(nameof(MaxLatency));
        }

        private readonly Dictionary<ushort, Action<PacketReader, EndPoint>> _packetIdToPacketHandler = new();
        private readonly Queue<(Packet packet, EndPoint senderEndPoint)> _pendingPackets = new();
        private readonly Queue<Action> _pendingActions = new();
        private readonly byte[] _receiveBuffer = new byte[Packet.MaxSize];

        private Socket _socket;
        private float _packetLoss;
        private int _minLatency;
        private int _maxLatency;

        /// <summary>
        /// Starts listening for incoming packets.
        /// </summary>
        /// <param name="port">Port to listen on.</param>
        protected void StartListening(int port)
        {
            if (IsListening) throw new InvalidOperationException("Could not start listening as node is already listening.");

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SendBufferSize = SendBufferSize;
            _socket.ReceiveBufferSize = ReceiveBufferSize;
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));

            new Thread(Listen)
            {
                Name = $"{GetType()}::{nameof(Listen)}",
                IsBackground = true
            }.Start();
        }

        private void Listen()
        {
            Log.Info($"Starting thread '{Thread.CurrentThread.Name}'...");

            while (true)
            {
                try
                {
                    if (_socket is null) break;

                    var senderEndPoint = AnyEndPoint;
                    var bytesReceived = _socket.ReceiveFrom(_receiveBuffer, ref senderEndPoint);
                    if (bytesReceived > 0) ReceiveAsync(bytesReceived, senderEndPoint);
                }
                catch (ObjectDisposedException)
                {
                    Log.Info("Socket has been disposed.");
                    break;
                }
                catch (SocketException e)
                {
                    // Socket has been forcefully disposed while receiving.
                    if (e.SocketErrorCode == SocketError.Interrupted) break;

                    // This is a known ICMP message received when destination port is unreachable.
                    // This occurs when remote end-point disconnects without sending the disconnect
                    // packet (or if disconnect packet did not arrive). This error does not need to
                    // be handled as the connection will get cleaned automatically once it times out.
                    if (e.SocketErrorCode == SocketError.ConnectionReset) continue;

                    Log.Warning($"Socket exception {e.ErrorCode}, {e.SocketErrorCode}: {e.Message}");
                }
                catch (Exception e)
                {
                    Log.Error($"Unexpected exception: {e}");
                }
            }

            Log.Info($"Stopping thread '{Thread.CurrentThread.Name}'...");
        }

        private async void ReceiveAsync(int bytesReceived, EndPoint senderEndPoint)
        {
            if (PacketLoss > 0 && Random.NextDouble() < PacketLoss) return;

            if (MaxLatency == 0)
            {
                Receive(_receiveBuffer.AsSpan(..bytesReceived), senderEndPoint);
            }
            else
            {
                // We need to copy receive buffer as it is going to get overwritten by new incoming packets.
                // However, this is totally fine as this branch is only executing when using the simulation.
                var receiveBufferCopy = new byte[bytesReceived];
                Array.Copy(_receiveBuffer, receiveBufferCopy, bytesReceived);

                await Task.Delay(Random.Next(MinLatency, MaxLatency + 1));
                Receive(receiveBufferCopy.AsSpan(), senderEndPoint);
            }
        }

        /// <summary>
        /// Processes received datagram and performs specific action based on the datagram contents.
        /// </summary>
        /// <param name="datagram">Read-only span containing datagram bytes.</param>
        /// <param name="senderEndPoint">Specifies from where datagram came from.</param>
        protected abstract void Receive(ReadOnlySpan<byte> datagram, EndPoint senderEndPoint);

        /// <summary>
        /// Handles the case of connection getting timed-out.
        /// </summary>
        /// <param name="connection">Connection that timed-out.</param>
        internal abstract void Timeout(Connection connection);

        /// <summary>
        /// Sends outgoing packet to the specified end-point.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        /// <param name="receiverEndPoint">Where to send the packet to.</param>
        /// <returns><c>true</c> if packet was successfully sent, <c>false</c> otherwise.</returns>
        public bool Send(Packet packet, EndPoint receiverEndPoint)
        {
            if (packet.Writer.Position > Packet.MaxSize)
            {
                Log.Error($"Packet exceeded maximum size of {Packet.MaxSize} bytes (has {packet.Writer.Position} bytes).");
                return false;
            }

            _socket.SendTo(packet.Buffer, offset: 0, packet.Writer.Position, SocketFlags.None, receiverEndPoint);
            return true;
        }

        /// <summary>
        /// Stop listening for incoming packets.
        /// </summary>
        protected void StopListening()
        {
            if (!IsListening) throw new InvalidOperationException("Could not stop listening as node is already not listening.");

            _socket.Dispose();
            _socket = null;
        }

        /// <summary>
        /// Enqueues a packet that will be handled on the next <see cref="Tick"/> method call.
        /// </summary>
        internal void EnqueuePendingPacket(Packet packet, EndPoint senderEndPoint)
        {
            if (packet is null) throw new ArgumentNullException(nameof(packet));
            if (senderEndPoint is null) throw new ArgumentNullException(nameof(senderEndPoint));

            lock (_pendingPackets) _pendingPackets.Enqueue((packet, senderEndPoint));
        }

        /// <summary>
        /// Enqueues an action that will be executed on the next <see cref="Tick"/> method call.
        /// </summary>
        protected void EnqueuePendingAction(Action action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));

            lock (_pendingActions) _pendingActions.Enqueue(action);
        }

        /// <summary>
        /// Handles all of the packets and actions that have been enqueued since last time this method was called.
        /// </summary>
        public void Tick()
        {
            HandlePendingPackets();
            HandlePendingActions();
        }

        private void HandlePendingPackets()
        {
            lock (_pendingPackets)
            {
                while (_pendingPackets.Count > 0)
                {
                    var (packet, senderEndPoint) = _pendingPackets.Dequeue();
                    var packetId = packet.Reader.Read<ushort>();

                    if (!_packetIdToPacketHandler.TryGetValue(packetId, out var packetHandler))
                    {
                        Log.Error($"Could not handle packet (ID = {packetId}) as it does not have a registered handler.");
                        packet.Return();
                        continue;
                    }

                    packetHandler(packet.Reader, senderEndPoint);
                    packet.Return();
                }
            }
        }

        private void HandlePendingActions()
        {
            lock (_pendingActions)
            {
                while (_pendingActions.Count > 0)
                {
                    _pendingActions.Dequeue()();
                }
            }
        }

        /// <summary>
        /// Registers a new packet handler for a packet with specific ID.
        /// </summary>
        public void RegisterPacketHandler(ushort packetId, Action<PacketReader, EndPoint> packetHandler) =>
            _packetIdToPacketHandler.Add(packetId, packetHandler);

        /// <summary>
        /// Returns true if packet handler for specific ID exists, false otherwise.
        /// </summary>
        public bool TryGetPacketHandler(ushort packedId, out Action<PacketReader, EndPoint> packetHandler) =>
            _packetIdToPacketHandler.TryGetValue(packedId, out packetHandler);

        /// <summary>
        /// Removes packet handler for specific packet ID.
        /// </summary>
        /// <returns><c>true</c> if packet handler has been successfully removed, <c>false</c> otherwise.</returns>
        public bool RemovePacketHandler(ushort packetId) =>
            _packetIdToPacketHandler.Remove(packetId);
    }
}
