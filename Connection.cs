using System;
using System.Net;
using Networking.Transport.Channels;
using Networking.Transport.Nodes;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a connection between two end-points. It is a higher level class that
    /// internally handles packet delivery methods and keeps track of packet statistics.
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
        /// If <c>true</c>, connection has been fully established.
        /// If <c>false</c>, connection is in process of connecting.
        /// </summary>
        public bool IsConnected { get; internal set; }

        private readonly Channel _unreliableChannel = new UnreliableChannel();
        private readonly Channel _sequencedChannel = new SequencedChannel();
        private readonly Channel _reliableChannel = new ReliableChannel();

        public Connection(Node node, EndPoint remoteEndPoint, bool isConnected)
        {
            Node = node;
            RemoteEndPoint = remoteEndPoint;
            IsConnected = isConnected;

            Node.Send(Packet.Get(isConnected ? HeaderType.ConnectApproved : HeaderType.Connect), RemoteEndPoint);
        }

        public void Send(Packet packet, bool returnPacketToPool = true)
        {
            GetChannel(packet.Buffer[0]).PreparePacketForSending(packet);
            Node.Send(packet, RemoteEndPoint, returnPacketToPool);
        }

        public Packet Receive(byte[] datagram, int bytesReceived) =>
            GetChannel(datagram[0]).PreparePacketForHandling(datagram, bytesReceived);

        private Channel GetChannel(byte channelId) => channelId switch
        {
            (int) HeaderType.UnreliableData => _unreliableChannel,
            (int) HeaderType.SequencedData => _sequencedChannel,
            (int) HeaderType.ReliableData => _reliableChannel,
            _ => throw new ArgumentException($"Channel with id {channelId} does not exist.")
        };

        public void Close() => Node.Send(Packet.Get(HeaderType.Disconnect), RemoteEndPoint);
    }
}
