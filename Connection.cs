using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using Networking.Transport.Channels;
using Networking.Transport.Nodes;
using static System.Threading.Timeout;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a connection between two end-points. It is a higher level class that
    /// internally handles different channels and keeps track of packet statistics.
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Duration between two consecutive ping packets, in milliseconds.
        /// </summary>
        private const int PingPeriod = 1000;

        /// <summary>
        /// If valid ping response is not received for this duration of time
        /// (in milliseconds), connection is going to time-out.
        /// </summary>
        private const int PingTimeout = 10_000;

        /// <summary>
        /// Factor used to apply exponential smoothing in order to calculate
        /// the value of <see cref="SmoothRoundTripTime"/>.
        /// </summary>
        private const double RttSmoothingFactor = 0.618;

        /// <summary>
        /// Returns round-trip time with applied exponential smoothing.
        /// </summary>
        public double SmoothRoundTripTime { get; private set; }

        /// <summary>
        /// Returns the most recently calculated round-trip time, in milliseconds.
        /// </summary>
        public double RoundTripTime { get; private set; }

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
        public IReadOnlyList<Channel> Channels => _channels;

        /// <summary>
        /// If <c>true</c>, connection has been fully established.
        /// If <c>false</c>, connection is in process of connecting.
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            internal set
            {
                _isConnected = value;
                var dueTime = _isConnected ? 0 : Infinite;
                var period = _isConnected ? PingPeriod : Infinite;
                _pingTimer.Change(dueTime, period);
            }
        }

        private bool _isConnected;
        private DateTime _lastPingResponseTime;

        private ushort _pingId;
        private readonly Timer _pingTimer;
        private readonly Stopwatch _pingStopwatch = new();
        private readonly Channel[] _channels = new Channel[Enum.GetValues(typeof(Delivery)).Length];

        internal Connection(Node node, EndPoint remoteEndPoint, bool isConnected)
        {
            _channels[(int) Delivery.Unreliable] = new UnreliableChannel(connection: this) {Name = nameof(Delivery.Unreliable)};
            _channels[(int) Delivery.Sequenced] = new SequencedChannel(connection: this) {Name = nameof(Delivery.Sequenced)};
            _channels[(int) Delivery.ReliableUnordered] = new ReliablePacketChannel(connection: this, isOrdered: false) {Name = nameof(Delivery.ReliableUnordered)};
            _channels[(int) Delivery.Reliable] = new ReliablePacketChannel(connection: this, isOrdered: true) {Name = nameof(Delivery.Reliable)};
            _channels[(int) Delivery.FragmentedUnordered] = new ReliableFragmentChannel(connection: this, isOrdered: false) {Name = nameof(Delivery.FragmentedUnordered)};
            _channels[(int) Delivery.Fragmented] = new ReliableFragmentChannel(connection: this, isOrdered: true) {Name = nameof(Delivery.Fragmented)};

            _pingTimer = new Timer(_ => SendPing());
            _lastPingResponseTime = DateTime.UtcNow;

            Node = node;
            RemoteEndPoint = remoteEndPoint;
            IsConnected = isConnected;

            Node.Send(Packet.Get(isConnected ? HeaderType.ConnectApproved : HeaderType.Connect), RemoteEndPoint);
        }

        public void Send(Packet packet, bool returnPacketToPool = true) =>
            GetChannel(packet.Buffer[0]).Send(packet, returnPacketToPool);

        internal void ReceiveData(byte[] datagram, int bytesReceived) =>
            GetChannel(datagram[0]).Receive(datagram, bytesReceived);

        internal void ReceiveAcknowledgement(byte[] datagram) =>
            GetChannel(datagram[0]).ReceiveAcknowledgement(datagram);

        private Channel GetChannel(byte header)
        {
            var channelId = header >> 4;

            if (channelId >= _channels.Length)
                throw new ArgumentException($"Channel with ID {channelId} does not exist.");

            return _channels[channelId];
        }

        private void SendPing()
        {
            if ((DateTime.UtcNow - _lastPingResponseTime).TotalMilliseconds > PingTimeout)
            {
                Timeout();
                Log.Info($"Connection timed-out: Ping response was not received in over {PingTimeout} ms.");
                return;
            }

            var pingPacket = Packet.Get(HeaderType.Ping);
            pingPacket.Writer.Write(++_pingId);
            Node.Send(pingPacket, RemoteEndPoint);

            _pingStopwatch.Restart();
        }

        internal void ReceivePing(byte[] datagram)
        {
            var pongPacket = Packet.Get(HeaderType.Pong);
            pongPacket.Writer.Write(datagram.Read<ushort>(offset: 1));
            Node.Send(pongPacket, RemoteEndPoint);
        }

        internal void ReceivePong(byte[] datagram)
        {
            var pongId = datagram.Read<ushort>(offset: 1);
            if (pongId != _pingId) return;

            RoundTripTime = _pingStopwatch.Elapsed.TotalMilliseconds;
            SmoothRoundTripTime = RttSmoothingFactor * RoundTripTime + (1 - RttSmoothingFactor) * SmoothRoundTripTime;

            _lastPingResponseTime = DateTime.UtcNow;
        }

        internal void Close(bool sendDisconnectPacket)
        {
            if (sendDisconnectPacket)
                Node.Send(Packet.Get(HeaderType.Disconnect), RemoteEndPoint);

            _pingTimer.Dispose();
        }

        /// <summary>
        /// Called each time connection gets timed-out. This could happen for multiple
        /// reasons, such as not receiving valid ping response for an extended period
        /// of time, or an external component detected faulty connection.
        /// </summary>
        internal void Timeout() => Node.Timeout(connection: this);
    }
}
