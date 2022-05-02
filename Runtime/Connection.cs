using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Link.Channels;
using Link.Nodes;

namespace Link
{
    /// <summary>
    /// Represents a connection between two end-points. It is a higher level class that
    /// internally handles different channels and keeps track of packet statistics.
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Represents a method that creates connect packet by filling it with required data.
        /// </summary>
        public delegate void ConnectPacketFactory(PacketWriter connectPacketWriter);
        
        /// <summary>
        /// Channel slots from 0 to this value are reserved and cannot be changed by the user.
        /// This channel range defines channels for all of the basic delivery methods, which
        /// relieves the user from having to declare any channels (making custom channels an
        /// optional feature).
        /// </summary>
        private const byte MaxReservedChannelId = 15;
        
        /// <summary>
        /// Underlying node that this connection belongs to.
        /// </summary>
        public Node Node { get; }

        /// <summary>
        /// Remote end-point to which this connection is pointing to.
        /// </summary>
        public EndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Maximum number of connect attempts before considering remote host as unreachable.
        /// </summary>
        public int MaxConnectAttempts { get; set; } = 5;

        /// <summary>
        /// Delay between consecutive connect attempts, in milliseconds.
        /// </summary>
        public int DelayBetweenConnectAttempts { get; set; } = 1000;

        /// <summary>
        /// Duration between two consecutive ping packets, in milliseconds.
        /// </summary>
        public int PeriodDuration { get; set; } = 1000;

        /// <summary>
        /// If ping response is not received for this duration (in milliseconds), connection is going to timeout.
        /// </summary>
        public int TimeoutDuration { get; set; } = 10_000;

        /// <summary>
        /// Weight used for calculating the value of <see cref="SmoothRoundTripTime"/>.
        /// </summary>
        public double SmoothingFactor { get; set; } = 0.125;

        /// <summary>
        /// Weight used for calculating the value of <see cref="RoundTripTimeDeviation"/>.
        /// </summary>
        public double DeviationFactor { get; set; } = 0.25;

        /// <summary>
        /// Returns round-trip time with applied exponential smoothing.
        /// </summary>
        public double SmoothRoundTripTime { get; private set; }

        /// <summary>
        /// Returns the most recently calculated round-trip time, in milliseconds.
        /// </summary>
        public double RoundTripTime { get; private set; }

        /// <summary>
        /// Returns deviation of the round-trip time.
        /// </summary>
        public double RoundTripTimeDeviation { get; private set; }

        /// <summary>
        /// Returns current state of this connection.
        /// </summary>
        public State CurrentState { get; private set; }

        private readonly Channel[] _channels = new Channel[byte.MaxValue];
        private readonly Stopwatch _rttStopwatch = new();
        private readonly Timer _sendPingTimer;

        private uint _lastPingRequestId;
        private uint _lastPingResponseId;
        private DateTime _lastPingResponseTime;

        internal Connection(Node node, EndPoint remoteEndPoint)
        {
            InitializeReservedChannels();
            _sendPingTimer = new Timer(_ => SendPing());

            Node = node;
            RemoteEndPoint = remoteEndPoint;
            CurrentState = State.Disconnected;
        }

        private void InitializeReservedChannels()
        {
            InitReservedChannel(Delivery.Unreliable, new UnreliableChannel(connection: this));
            InitReservedChannel(Delivery.Sequenced, new SequencedChannel(connection: this));
            InitReservedChannel(Delivery.ReliableUnordered, new ReliablePacketChannel(connection: this, isOrdered: false));
            InitReservedChannel(Delivery.Reliable, new ReliablePacketChannel(connection: this, isOrdered: true));
            InitReservedChannel(Delivery.FragmentedUnordered, new ReliableFragmentChannel(connection: this, isOrdered: false));
            InitReservedChannel(Delivery.Fragmented, new ReliableFragmentChannel(connection: this, isOrdered: true));

            void InitReservedChannel(Delivery delivery, Channel channel)
            {
                channel.Name = delivery.ToString();
                _channels[(int) delivery] = channel;
            }
        }
        
        public Channel this[byte channelId]
        {
            get => _channels[channelId];
            set
            {
                if (channelId <= MaxReservedChannelId)
                    throw new InvalidOperationException($"Failed to set channel (ID = {channelId}) as slots from 0 to {MaxReservedChannelId} are reserved.");

                if (_channels[channelId] is not null)
                    throw new InvalidOperationException($"Channel slot (ID = {channelId}) is already filled by channel named '{_channels[channelId].Name}'.");

                _channels[channelId] = value ?? throw new InvalidOperationException("Cannot set null channel.");
            }
        }

        internal async void Establish(ConnectPacketFactory connectPacketFactory = null)
        {
            if (MaxConnectAttempts <= 0) throw new ArgumentException($"'{nameof(MaxConnectAttempts)}' must be a positive value.");
            if (DelayBetweenConnectAttempts <= 0) throw new ArgumentException($"'{nameof(DelayBetweenConnectAttempts)}' must be a positive value.");

            var connectPacket = Packet.Get(HeaderType.Connect);
            connectPacketFactory?.Invoke(new PacketWriter(connectPacket));

            for (var attempt = 0; attempt < MaxConnectAttempts; attempt++)
            {
                Node.Send(connectPacket, RemoteEndPoint);
                CurrentState = State.Connecting;

                await Task.Delay(DelayBetweenConnectAttempts);
                if (CurrentState == State.Connecting) continue;

                connectPacket.Return();
                return;
            }

            connectPacket.Return();
            Timeout($"Exceeded maximum connect attempts of {MaxConnectAttempts} while connecting to {RemoteEndPoint}.");
        }

        internal void ReceiveConnect()
        {
            var connectApprovedPacket = Packet.Get(HeaderType.ConnectApproved);
            Node.Send(connectApprovedPacket, RemoteEndPoint);
            connectApprovedPacket.Return();

            ReceiveConnectApproved();
        }

        internal void ReceiveConnectApproved()
        {
            _lastPingResponseTime = DateTime.UtcNow;
            _sendPingTimer.Change(dueTime: 0, period: PeriodDuration);
            CurrentState = State.Connected;
        }

        internal void SendData(Packet packet) =>
            RequireChannel(packet.Buffer[1]).Send(packet);

        internal void ReceiveData(PacketReader reader) =>
            RequireChannel(reader.Read<byte>()).Receive(reader);

        internal void ReceiveAcknowledgement(PacketReader reader) =>
            RequireChannel(reader.Read<byte>()).ReceiveAcknowledgement(reader);

        private Channel RequireChannel(byte id) =>
            _channels[id] ?? throw new ArgumentException($"Channel with ID {id} does not exist.");

        private void SendPing()
        {
            if ((DateTime.UtcNow - _lastPingResponseTime).TotalMilliseconds > TimeoutDuration)
            {
                Timeout($"Valid ping response was not received in over {TimeoutDuration} ms.");
                return;
            }

            var pingPacket = Packet.Get(HeaderType.Ping);
            pingPacket.Write(++_lastPingRequestId);
            Node.Send(pingPacket, RemoteEndPoint);
            pingPacket.Return();

            _rttStopwatch.Restart();
        }

        internal void ReceivePing(PacketReader reader)
        {
            var pongPacket = Packet.Get(HeaderType.Pong);
            pongPacket.Write(reader.Read<uint>());
            Node.Send(pongPacket, RemoteEndPoint);
            pongPacket.Return();
        }

        internal void ReceivePong(PacketReader reader)
        {
            var responseId = reader.Read<uint>();
            if (responseId <= _lastPingResponseId) return;

            _lastPingResponseId = responseId;
            _lastPingResponseTime = DateTime.UtcNow;

            RoundTripTime = (_lastPingRequestId - _lastPingResponseId) * PeriodDuration + _rttStopwatch.Elapsed.TotalMilliseconds;
            SmoothRoundTripTime = (1 - SmoothingFactor) * SmoothRoundTripTime + SmoothingFactor * RoundTripTime;
            RoundTripTimeDeviation = (1 - DeviationFactor) * RoundTripTimeDeviation + DeviationFactor * Math.Abs(RoundTripTime - SmoothRoundTripTime);
        }

        /// <summary>
        /// Called each time connection gets timed-out. This could happen for multiple
        /// reasons, such as not receiving valid ping response for an extended period
        /// of time, or an external component detected faulty connection. This method
        /// "pretends" that timeout packet was sent from the remote end-point.
        /// </summary>
        internal void Timeout(string timeoutCause)
        {
            Node.ConsumeOrEnqueuePacket(Packet.Get(HeaderType.Timeout), RemoteEndPoint);
            Log.Info($"Connection timed-out: {timeoutCause}");
        }
        
        /// <summary>
        /// Sends disconnect packet to the remote end-point and disposes all of the used resources.
        /// </summary>
        internal void Close()
        {
            if (CurrentState == State.Disconnected) return;
            
            var disconnectPacket = Packet.Get(HeaderType.Disconnect);
            Node.Send(disconnectPacket, RemoteEndPoint);
            disconnectPacket.Return();

            _sendPingTimer.Dispose();
            CurrentState = State.Disconnected;
        }

        /// <summary>
        /// Enumeration of all the possible states a connection can be found in.
        /// </summary>
        public enum State
        {
            Disconnected,
            Connecting,
            Connected,
        }
    }
}
