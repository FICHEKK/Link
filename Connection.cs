using System;
using System.Net;
using Networking.Transport.Channels;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a connection between two end-points. It is a higher level class that
    /// internally handles packet delivery methods and keeps track of packet statistics.
    /// </summary>
    public class Connection
    {
        public State CurrentState { get; internal set; }
        public EndPoint RemoteEndPoint { get; internal set; }

        private readonly Channel _unreliableChannel = new UnreliableChannel();
        private readonly Channel _sequencedChannel = new SequencedChannel();
        private readonly Channel _reliableChannel = new ReliableChannel();

        public void PreparePacketForSending(Packet packet) =>
            GetChannel(packet.Buffer[0]).PreparePacketForSending(packet);

        public Packet PreparePacketForHandling(byte[] datagram, int bytesReceived) =>
            GetChannel(datagram[0]).PreparePacketForHandling(datagram, bytesReceived);

        private Channel GetChannel(byte channelId) => channelId switch
        {
            (int) HeaderType.UnreliableData => _unreliableChannel,
            (int) HeaderType.SequencedData => _sequencedChannel,
            (int) HeaderType.ReliableData => _reliableChannel,
            _ => throw new ArgumentException($"Channel with id {channelId} does not exist.")
        };

        /// <summary>
        /// Defines all of the states that a connection can be in.
        /// </summary>
        public enum State
        {
            Disconnected,
            Connecting,
            Connected
        }
    }
}
