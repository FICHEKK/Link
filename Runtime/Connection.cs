using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
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
        /// Channel slots from this value to 255 are reserved and cannot be changed by the user.
        /// This channel range defines channels for all of the basic delivery methods, which
        /// relieves the user from having to declare any channels (making custom channels an
        /// optional feature).
        /// </summary>
        private const byte MinReservedChannelId = (byte) Delivery.Unreliable;
        
        /// <summary>
        /// Underlying node that this connection belongs to.
        /// </summary>
        internal Node Node { get; }

        /// <summary>
        /// Remote end-point to which this connection is pointing to.
        /// </summary>
        public EndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Duration between sending two consecutive ping packets, in milliseconds.
        /// </summary>
        /// <remarks>This value cannot be negative.</remarks>
        public int PeriodDuration
        {
            get => _periodDuration;
            set => _periodDuration = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(PeriodDuration));
        }

        /// <summary>
        /// Backing field of <see cref="PeriodDuration"/> property.
        /// </summary>
        private int _periodDuration = 1000;

        /// <summary>
        /// If ping response is not received for this duration (in milliseconds), connection is going to timeout.
        /// </summary>
        /// <remarks>If set to -1, timeout will be disabled.</remarks>
        public int TimeoutDuration
        {
            get => _timeoutDuration;
            set => _timeoutDuration = value >= -1 ? value : throw new ArgumentOutOfRangeException(nameof(TimeoutDuration));
        }

        /// <summary>
        /// Backing field of <see cref="TimeoutDuration"/> property.
        /// </summary>
        private int _timeoutDuration = 10_000;

        /// <summary>
        /// Weight used for calculating the value of <see cref="SmoothRoundTripTime"/>.
        /// </summary>
        public double SmoothingFactor { get; set; } = 0.125;

        /// <summary>
        /// Weight used for calculating the value of <see cref="RoundTripTimeDeviation"/>.
        /// </summary>
        public double DeviationFactor { get; set; } = 0.25;
        
        /// <summary>
        /// Returns the most recently measured round-trip time (in milliseconds) or -1
        /// if not yet measured.
        /// </summary>
        public double RoundTripTime { get; private set; } = -1;

        /// <summary>
        /// Returns round-trip time (in milliseconds) with applied exponential smoothing
        /// or -1 if round-trip time hasn't been measured yet.
        /// </summary>
        public double SmoothRoundTripTime { get; private set; } = -1;

        /// <summary>
        /// Returns deviation of the round-trip time or -1 if not yet measured.
        /// </summary>
        public double RoundTripTimeDeviation { get; private set; } = -1;
        
        /// <summary>
        /// Total number of packets sent through this connection.
        /// </summary>
        public long PacketsSent { get; private set; }

        /// <summary>
        /// Total number of bytes sent through this connection.
        /// </summary>
        public long BytesSent { get; private set; }

        /// <summary>
        /// Total number of packets received on this connection.
        /// </summary>
        public long PacketsReceived { get; private set; }

        /// <summary>
        /// Total number of bytes received on this connection.
        /// </summary>
        public long BytesReceived { get; private set; }
        
        /// <summary>
        /// Returns the number of channels that this connection currently has.
        /// </summary>
        public int ChannelCount { get; private set; }

        /// <summary>
        /// Returns current state of this connection.
        /// </summary>
        internal State CurrentState { get; private set; }

        private readonly Channel?[] _channels = new Channel[byte.MaxValue];
        private readonly Stopwatch _rttStopwatch = new();
        private readonly Timer _sendPingTimer;

        private uint _lastPingRequestId;
        private uint _lastPingResponseId;
        private DateTime _lastPingResponseTime;

        internal Connection(Node node, EndPoint remoteEndPoint, State initialState)
        {
            InitializeReservedChannels();
            _sendPingTimer = new Timer(_ => SendPing());

            Node = node;
            RemoteEndPoint = remoteEndPoint;
            CurrentState = initialState;
        }

        private void InitializeReservedChannels()
        {
            InitReservedChannel(Delivery.Unreliable, new UnreliableChannel(connection: this));
            InitReservedChannel(Delivery.Sequenced, new SequencedChannel(connection: this));
            InitReservedChannel(Delivery.Reliable, new ReliableChannel(connection: this));

            void InitReservedChannel(Delivery delivery, Channel channel)
            {
                channel.Name = delivery.ToString();
                _channels[(int) delivery] = channel;
                ChannelCount++;
            }
        }
        
        public Channel? this[byte channelId]
        {
            get => _channels[channelId];
            set
            {
                var channel = _channels[channelId];
                
                if (channel is not null)
                    throw new InvalidOperationException($"Channel slot (ID = {channelId}) is already filled by channel named '{channel.Name}'.");
                
                if (channelId >= MinReservedChannelId)
                    throw new InvalidOperationException($"Failed to set channel (ID = {channelId}) as slots from {MinReservedChannelId} to 255 are reserved.");

                _channels[channelId] = value ?? throw new InvalidOperationException("Cannot set null channel.");
                ChannelCount++;
            }
        }

        internal void ReceiveConnectApproved()
        {
            _lastPingResponseTime = DateTime.UtcNow;
            _sendPingTimer.Change(dueTime: 0, period: PeriodDuration);
            CurrentState = State.Connected;
        }

        internal void SendData(Packet packet)
        {
            RequireChannel(packet.Buffer.Bytes[1]).Send(packet);
            PacketsSent++;
            BytesSent += packet.Size;
        }

        internal void ReceiveData(ReadOnlyPacket packet)
        {
            RequireChannel(packet.Read<byte>()).Receive(packet);
            PacketsReceived++;
            BytesReceived += packet.Size;
        }

        internal void ReceiveAcknowledgement(ReadOnlyPacket packet) =>
            RequireChannel(packet.Read<byte>()).ReceiveAcknowledgement(packet);

        private Channel RequireChannel(byte id) =>
            _channels[id] ?? throw new ArgumentException($"Channel with ID {id} does not exist.");

        private void SendPing()
        {
            if (TimeoutDuration >= 0 && (DateTime.UtcNow - _lastPingResponseTime).TotalMilliseconds > TimeoutDuration)
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

        internal void ReceivePing(ReadOnlyPacket packet)
        {
            var pongPacket = Packet.Get(HeaderType.Pong);
            pongPacket.Write(packet.Read<uint>());
            Node.Send(pongPacket, RemoteEndPoint);
            pongPacket.Return();
        }

        internal void ReceivePong(ReadOnlyPacket packet)
        {
            var responseId = packet.Read<uint>();
            if (responseId <= _lastPingResponseId) return;

            _lastPingResponseId = responseId;
            _lastPingResponseTime = DateTime.UtcNow;

            var isFirstMeasuring = RoundTripTime < 0;
            RoundTripTime = (_lastPingRequestId - _lastPingResponseId) * PeriodDuration + _rttStopwatch.Elapsed.TotalMilliseconds;

            if (isFirstMeasuring)
            {
                SmoothRoundTripTime = RoundTripTime;
                RoundTripTimeDeviation = 0;
            }
            else
            {
                SmoothRoundTripTime = (1 - SmoothingFactor) * SmoothRoundTripTime + SmoothingFactor * RoundTripTime;
                RoundTripTimeDeviation = (1 - DeviationFactor) * RoundTripTimeDeviation + DeviationFactor * Math.Abs(RoundTripTime - SmoothRoundTripTime);
            }
        }

        /// <summary>
        /// Called each time connection gets timed-out. This could happen for multiple
        /// reasons, such as not receiving valid ping response for an extended period
        /// of time, or an external component detected faulty connection. This method
        /// "pretends" that timeout packet was sent from the remote end-point.
        /// </summary>
        internal void Timeout(string timeoutCause)
        {
            Node.ConsumeOrEnqueuePacket(Packet.Get(HeaderType.Timeout).Buffer, RemoteEndPoint);
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
            
            foreach (var channel in _channels) channel?.Close();
        }

        /// <summary>
        /// Enumeration of all the possible states a connection can be found in.
        /// </summary>
        internal enum State
        {
            Disconnected,
            Connecting,
            Connected,
        }
    }
}
