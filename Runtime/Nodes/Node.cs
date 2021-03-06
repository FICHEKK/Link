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
    public abstract class Node : IDisposable
    {
        /// <summary>
        /// Defines a method that handles an event that was raised by an instance of <see cref="Node"/>.
        /// </summary>
        /// <typeparam name="TEventArgs">Type of data associated with the event.</typeparam>
        public delegate void EventHandler<in TEventArgs>(TEventArgs eventArgs) where TEventArgs : class;
        
        /// <summary>
        /// Default socket send and receive buffer size.
        /// </summary>
        private const int DefaultBufferSize = 1024 * 1024;

        /// <summary>
        /// Random instance used for network simulation purposes.
        /// </summary>
        private static readonly Random Random = new();

        /// <summary>
        /// Returns the local end-point on which this node is listening on, or <c>null</c> if not listening.
        /// </summary>
        public IPEndPoint? LocalEndPoint => _socket?.LocalEndPoint as IPEndPoint;
        
        /// <summary>
        /// If <c>true</c>, packets will be automatically and immediately processed as they are received. 
        /// This means packets will be processed on a thread that is not under your control.
        /// <br/><br/>
        /// If <c>false</c>, packets will be enqueued as they are received. Packets are processed with a
        /// call to the <see cref="Tick"/> method, which gives you the control to choose on which thread
        /// packets should be processed.
        /// <br/><br/>
        /// This option defaults to <c>true</c>.
        /// </summary>
        public bool IsAutomatic { get; set; } = true;

        /// <summary>
        /// Returns <c>true</c> if this node is currently listening for incoming packets.
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
        /// Initializes newly created connections. Can be used to define custom channels or
        /// to set connection settings to values that differ from the default ones.
        /// </summary>
        public Action<Connection>? ConnectionInitializer { get; set; }

        /// <summary>
        /// Defines the probability of a packet being lost.
        /// This property should only be used only for testing purposes.
        /// </summary>
        /// <remarks>Value must be in range from 0 to 1.</remarks>
        public double PacketLoss
        {
            get => _packetLoss;
            set => _packetLoss = value is >= 0 and <= 1 ? value : throw new ArgumentOutOfRangeException(nameof(PacketLoss));
        }

        /// <summary>
        /// Minimum additional delay (in ms) before processing received packet.
        /// If set to greater than <see cref="MaxLatency"/>, <see cref="MaxLatency"/> is also going to be set to that value.
        /// </summary>
        public int MinLatency
        {
            get => _minLatency;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(MinLatency), $"{nameof(MinLatency)} must be positive.");

                if (value > MaxLatency)
                    _maxLatency = value;

                _minLatency = value;
            }
        }

        /// <summary>
        /// Maximum additional delay (in ms) before processing received packet.
        /// If set to lower than <see cref="MinLatency"/>, <see cref="MinLatency"/> is also going to be set to that value.
        /// </summary>
        public int MaxLatency
        {
            get => _maxLatency;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(MaxLatency), $"{nameof(MaxLatency)} must be positive.");

                if (value < MinLatency)
                    _minLatency = value;

                _maxLatency = value;
            }
        }

        private Queue<(Buffer packet, EndPoint senderEndPoint)> _producerPackets = new();
        private Queue<(Buffer packet, EndPoint senderEndPoint)> _consumerPackets = new();

        private Socket? _socket;
        private double _packetLoss;
        private int _minLatency;
        private int _maxLatency;

        /// <summary>
        /// Starts listening for incoming packets on the given port.
        /// </summary>
        protected void Listen(ushort port)
        {
            if (IsListening) throw new InvalidOperationException("Could not start listening as node is already listening.");

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SendBufferSize = SendBufferSize;
            _socket.ReceiveBufferSize = ReceiveBufferSize;
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));

            new Thread(Listen) { Name = $"{GetType()}::{nameof(Listen)}", IsBackground = true }.Start();
        }

        private void Listen()
        {
            Log.Info($"Starting thread '{Thread.CurrentThread.Name}'...");
            
            var receiveBuffer = new byte[Packet.BufferSize];
            var senderEndPoint = (EndPoint) new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    if (_socket is null) break;

                    var bytesReceived = _socket.ReceiveFrom(receiveBuffer, ref senderEndPoint);
                    if (bytesReceived > 0) ReceiveAsync(receiveBuffer, bytesReceived, senderEndPoint);
                }
                catch (ObjectDisposedException)
                {
                    Log.Info($"Socket has been disposed on thread '{Thread.CurrentThread.Name}'.");
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

                    Log.Warning($"Socket exception {e.ErrorCode}, {e.SocketErrorCode}: '{e.Message}' on thread '{Thread.CurrentThread.Name}'.");
                }
                catch (Exception e)
                {
                    Log.Error($"Unexpected exception: '{e}' on thread '{Thread.CurrentThread.Name}'.");
                }
            }

            Log.Info($"Stopping thread '{Thread.CurrentThread.Name}'...");
        }

        private async void ReceiveAsync(byte[] receiveBuffer, int bytesReceived, EndPoint senderEndPoint)
        {
            if (PacketLoss > 0 && Random.NextDouble() < PacketLoss) return;

            var packet = Buffer.From(receiveBuffer, bytesReceived);
            if (MaxLatency > 0) await Task.Delay(Random.Next(MinLatency, MaxLatency + 1));

            ConsumeOrEnqueuePacket(packet, senderEndPoint);
        }

        /// <summary>
        /// Consumes or enqueues a given packet (action is decided by <see cref="IsAutomatic"/>).
        /// </summary>
        internal void ConsumeOrEnqueuePacket(Buffer packet, EndPoint senderEndPoint)
        {
            if (IsAutomatic)
            {
                Consume(new ReadOnlyPacket(packet), senderEndPoint);
                packet.Return();
                return;
            }

            lock (_producerPackets) _producerPackets.Enqueue((packet, senderEndPoint));
        }

        /// <summary>
        /// Sends outgoing packet to the specified end-point.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        /// <param name="receiverEndPoint">Where to send the packet to.</param>
        internal void Send(Packet packet, EndPoint receiverEndPoint)
        {
            if (packet.Size > Packet.MaxSize)
                throw new InvalidOperationException($"Packet exceeded maximum size of {Packet.MaxSize} bytes (has {packet.Size} bytes).");

            if (_socket is null)
                throw new InvalidOperationException("Cannot send packet as socket has not been initialized.");

            _socket.SendTo(packet.Buffer.Bytes, offset: 0, packet.Buffer.Size, SocketFlags.None, receiverEndPoint);
        }

        /// <summary>
        /// Handles all of the packets that have been enqueued since last time this method was called.
        /// This method should be called only when <see cref="IsAutomatic"/> is set to <c>false</c>.
        /// </summary>
        public void Tick()
        {
            if (IsAutomatic) throw new InvalidOperationException($"'{nameof(Tick)}' called with automatic mode on.");
            
            lock (_producerPackets)
            lock (_consumerPackets)
            {
                // In order to not block producer's queue while consuming packets, we simply swap queues.
                (_producerPackets, _consumerPackets) = (_consumerPackets, _producerPackets);
            }
            
            lock (_consumerPackets)
            {
                while (_consumerPackets.Count > 0)
                {
                    var (packet, senderEndPoint) = _consumerPackets.Dequeue();
                    Consume(new ReadOnlyPacket(packet), senderEndPoint);
                    packet.Return();
                }
            }
        }
        
        /// <summary>
        /// Consumes received packet by performing specific action based on the packet contents.
        /// </summary>
        /// <param name="packet">Read-only view into packet being consumed.</param>
        /// <param name="senderEndPoint">Specifies from where the packet came from.</param>
        protected abstract void Consume(ReadOnlyPacket packet, EndPoint senderEndPoint);

        /// <summary>
        /// Performs the logic of receiving data-packet buffer.
        /// </summary>
        internal abstract void Receive(Buffer buffer, EndPoint senderEndPoint);

        /// <summary>
        /// Stop listening for incoming packets.
        /// </summary>
        public void Dispose()
        {
            Dispose(isDisposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs the actual logic of disposing resources.
        /// </summary>
        protected virtual void Dispose(bool isDisposing)
        {
            if (!isDisposing) return;
        
            _socket?.Dispose();
            _socket = null;
        }
    }
}
