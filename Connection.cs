using System;
using System.Collections.Generic;
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
        /// Underlying node that this connection belongs to.
        /// </summary>
        public Node Node { get; }

        /// <summary>
        /// Remote end-point to which this connection is pointing to.
        /// </summary>
        public EndPoint RemoteEndPoint { get; }

        /// <summary>
        /// Returns a read-only list of registered channels.
        /// </summary>
        public IEnumerable<Channel> Channels => _channels;

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

        private readonly Channel[] _channels = new Channel[Enum.GetValues(typeof(Delivery)).Length];
        private readonly Stopwatch _rttStopwatch;
        private readonly Timer _sendPingTimer;

        private uint _lastPingRequestId;
        private uint _lastPingResponseId;
        private DateTime _lastPingResponseTime;

        internal Connection(Node node, EndPoint remoteEndPoint)
        {
            InitializeChannels();
            _rttStopwatch = new Stopwatch();
            _sendPingTimer = new Timer(_ => SendPing());

            Node = node;
            RemoteEndPoint = remoteEndPoint;
            CurrentState = State.Disconnected;
        }

        private void InitializeChannels()
        {
            _channels[(int) Delivery.Unreliable] = new UnreliableChannel(connection: this) {Name = nameof(Delivery.Unreliable)};
            _channels[(int) Delivery.Sequenced] = new SequencedChannel(connection: this) {Name = nameof(Delivery.Sequenced)};
            _channels[(int) Delivery.ReliableUnordered] = new ReliablePacketChannel(connection: this, isOrdered: false) {Name = nameof(Delivery.ReliableUnordered)};
            _channels[(int) Delivery.Reliable] = new ReliablePacketChannel(connection: this, isOrdered: true) {Name = nameof(Delivery.Reliable)};
            _channels[(int) Delivery.FragmentedUnordered] = new ReliableFragmentChannel(connection: this, isOrdered: false) {Name = nameof(Delivery.FragmentedUnordered)};
            _channels[(int) Delivery.Fragmented] = new ReliableFragmentChannel(connection: this, isOrdered: true) {Name = nameof(Delivery.Fragmented)};
        }

        internal async void Establish(int maxAttempts, int delayBetweenAttempts)
        {
            if (maxAttempts <= 0) throw new ArgumentException($"'{nameof(maxAttempts)}' must be a positive value.");
            if (delayBetweenAttempts <= 0) throw new ArgumentException($"'{nameof(delayBetweenAttempts)}' must be a positive value.");

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var connectPacket = Packet.Get(HeaderType.Connect);
                Node.Send(connectPacket, RemoteEndPoint);
                connectPacket.Return();

                Log.Info($"Connecting to {RemoteEndPoint} - attempt {attempt}.");
                CurrentState = State.Connecting;

                await Task.Delay(delayBetweenAttempts);
                if (CurrentState != State.Connecting) return;
            }

            Node.Timeout(connection: this);
            Log.Info($"Connection timed-out: could not connect to {RemoteEndPoint} (exceeded maximum connect attempts of {maxAttempts}).");
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

        public void Send(Packet packet) =>
            GetChannel(packet.Buffer[1]).Send(packet);

        internal void ReceiveData(byte[] datagram, int bytesReceived) =>
            GetChannel(datagram[1]).Receive(datagram, bytesReceived);

        internal void ReceiveAcknowledgement(byte[] datagram) =>
            GetChannel(datagram[1]).ReceiveAcknowledgement(datagram);

        private Channel GetChannel(byte channelId) =>
            channelId < _channels.Length ? _channels[channelId] : throw new ArgumentException($"Channel with ID {channelId} does not exist.");

        private void SendPing()
        {
            if ((DateTime.UtcNow - _lastPingResponseTime).TotalMilliseconds > TimeoutDuration)
            {
                Log.Info($"Connection timed-out: Valid ping response was not received in over {TimeoutDuration} ms.");
                Timeout();
                return;
            }

            var pingPacket = Packet.Get(HeaderType.Ping);
            pingPacket.Writer.Write(++_lastPingRequestId);
            Node.Send(pingPacket, RemoteEndPoint);
            pingPacket.Return();

            _rttStopwatch.Restart();
        }

        internal void ReceivePing(byte[] datagram)
        {
            var pongPacket = Packet.Get(HeaderType.Pong);
            pongPacket.Writer.Write(datagram.Read<uint>(offset: 1));
            Node.Send(pongPacket, RemoteEndPoint);
            pongPacket.Return();
        }

        internal void ReceivePong(byte[] datagram)
        {
            var responseId = datagram.Read<uint>(offset: 1);
            if (responseId <= _lastPingResponseId) return;

            _lastPingResponseId = responseId;
            _lastPingResponseTime = DateTime.UtcNow;

            RoundTripTime = (_lastPingRequestId - _lastPingResponseId) * PeriodDuration + _rttStopwatch.Elapsed.TotalMilliseconds;
            SmoothRoundTripTime = (1 - SmoothingFactor) * SmoothRoundTripTime + SmoothingFactor * RoundTripTime;
            RoundTripTimeDeviation = (1 - DeviationFactor) * RoundTripTimeDeviation + DeviationFactor * Math.Abs(RoundTripTime - SmoothRoundTripTime);
        }

        internal void Close(bool sendDisconnectPacket)
        {
            if (sendDisconnectPacket)
            {
                var disconnectPacket = Packet.Get(HeaderType.Disconnect);
                Node.Send(disconnectPacket, RemoteEndPoint);
                disconnectPacket.Return();
            }

            _sendPingTimer.Dispose();
            CurrentState = State.Disconnected;
        }

        /// <summary>
        /// Called each time connection gets timed-out. This could happen for multiple
        /// reasons, such as not receiving valid ping response for an extended period
        /// of time, or an external component detected faulty connection.
        /// </summary>
        internal void Timeout() => Node.Timeout(connection: this);

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
