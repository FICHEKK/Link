using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
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
        /// Returns the most recently calculated round-trip time, in milliseconds.
        /// </summary>
        public long Ping { get; private set; }

        /// <summary>
        /// Underlying node that this connection belongs to.
        /// </summary>
        public Node Node { get; }

        /// <summary>
        /// Remote end-point to which this connection is pointing to.
        /// </summary>
        public EndPoint RemoteEndPoint { get; }

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
                var dueTime = _isConnected ? 0 : Timeout.Infinite;
                var period = _isConnected ? 1000 : Timeout.Infinite;
                _pingTimer.Change(dueTime, period);
            }
        }

        private bool _isConnected;
        private ushort _pingId;
        private readonly Timer _pingTimer;
        private readonly Stopwatch _pingStopwatch = new();

        // TODO - Don't hardcode channels.
        private readonly Channel _unreliableChannel;
        private readonly Channel _sequencedChannel;
        private readonly Channel _reliableChannel;

        internal Connection(Node node, EndPoint remoteEndPoint, bool isConnected)
        {
            _unreliableChannel = new UnreliableChannel(node, remoteEndPoint);
            _sequencedChannel = new SequencedChannel(node, remoteEndPoint);
            _reliableChannel = new ReliableChannel(node, remoteEndPoint);

            _pingTimer = new Timer(_ => SendPing());

            Node = node;
            RemoteEndPoint = remoteEndPoint;
            IsConnected = isConnected;

            Node.Send(Packet.Get(isConnected ? HeaderType.ConnectApproved : HeaderType.Connect), RemoteEndPoint);
        }

        public void Send(Packet packet, bool returnPacketToPool = true) =>
            GetChannel(packet.Buffer[0]).Send(packet, returnPacketToPool);

        internal void Receive(byte[] datagram, int bytesReceived) =>
            GetChannel(datagram[0]).Receive(datagram, bytesReceived);

        internal void ReceiveAcknowledgement(byte[] datagram) =>
            GetChannel(datagram[0]).ReceiveAcknowledgement(datagram);

        private Channel GetChannel(byte channelId) => channelId switch
        {
            (int) HeaderType.UnreliableData => _unreliableChannel,
            (int) HeaderType.SequencedData => _sequencedChannel,
            (int) HeaderType.ReliableData => _reliableChannel,
            _ => throw new ArgumentException($"Channel with id {channelId} does not exist.")
        };

        private void SendPing()
        {
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
            if (pongId == _pingId) Ping = _pingStopwatch.ElapsedMilliseconds;
        }

        internal void Close(bool sendDisconnectPacket)
        {
            if (sendDisconnectPacket)
                Node.Send(Packet.Get(HeaderType.Disconnect), RemoteEndPoint);

            _pingTimer.Dispose();
        }
    }
}
