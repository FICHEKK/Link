using System;
using System.Collections.Generic;
using System.Net;
using Networking.Transport.Channels;
using Networking.Transport.Nodes;

namespace Networking.Transport
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
        public IReadOnlyList<Channel> Channels => _channels;

        /// <summary>
        /// Returns round-trip time with applied exponential smoothing.
        /// </summary>
        public double SmoothRoundTripTime => _pingMeasurer.SmoothRoundTripTime;

        /// <summary>
        /// Returns the most recently calculated round-trip time, in milliseconds.
        /// </summary>
        public double RoundTripTime => _pingMeasurer.RoundTripTime;

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

                if (_isConnected)
                {
                    _pingMeasurer.StartMeasuring();
                }
                else
                {
                    _pingMeasurer.StopMeasuring();
                }
            }
        }

        private readonly Channel[] _channels = new Channel[Enum.GetValues(typeof(Delivery)).Length];
        private readonly PingMeasurer _pingMeasurer;
        private bool _isConnected;

        internal Connection(Node node, EndPoint remoteEndPoint, bool isConnected)
        {
            _channels[(int) Delivery.Unreliable] = new UnreliableChannel(connection: this) {Name = nameof(Delivery.Unreliable)};
            _channels[(int) Delivery.Sequenced] = new SequencedChannel(connection: this) {Name = nameof(Delivery.Sequenced)};
            _channels[(int) Delivery.ReliableUnordered] = new ReliablePacketChannel(connection: this, isOrdered: false) {Name = nameof(Delivery.ReliableUnordered)};
            _channels[(int) Delivery.Reliable] = new ReliablePacketChannel(connection: this, isOrdered: true) {Name = nameof(Delivery.Reliable)};
            _channels[(int) Delivery.FragmentedUnordered] = new ReliableFragmentChannel(connection: this, isOrdered: false) {Name = nameof(Delivery.FragmentedUnordered)};
            _channels[(int) Delivery.Fragmented] = new ReliableFragmentChannel(connection: this, isOrdered: true) {Name = nameof(Delivery.Fragmented)};

            _pingMeasurer = new PingMeasurer(connection: this);

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

        internal void ReceivePing(byte[] datagram) =>
            _pingMeasurer.ReceivePing(datagram);

        internal void ReceivePong(byte[] datagram) =>
            _pingMeasurer.ReceivePong(datagram);

        internal void Close(bool sendDisconnectPacket)
        {
            if (sendDisconnectPacket)
                Node.Send(Packet.Get(HeaderType.Disconnect), RemoteEndPoint);

            _pingMeasurer.Dispose();
        }

        /// <summary>
        /// Called each time connection gets timed-out. This could happen for multiple
        /// reasons, such as not receiving valid ping response for an extended period
        /// of time, or an external component detected faulty connection.
        /// </summary>
        internal void Timeout() => Node.Timeout(connection: this);
    }
}
