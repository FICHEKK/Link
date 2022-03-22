namespace Networking.Transport.Channels
{
    /// <summary>
    /// Defines the way data is sent and received.
    /// </summary>
    public abstract class Channel
    {
        /// <summary>
        /// Name associated with this channel.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Total number of packets sent through this channel.
        /// </summary>
        public long PacketsSent { get; private set; }

        /// <summary>
        /// Total number of bytes sent through this channel.
        /// </summary>
        public long BytesSent { get; private set; }

        /// <summary>
        /// Total number of packets received on this channel.
        /// </summary>
        public long PacketsReceived { get; private set; }

        /// <summary>
        /// Total number of bytes received on this channel.
        /// </summary>
        public long BytesReceived { get; private set; }

        /// <summary>
        /// Writes header information and sends given packet to the remote end-point.
        /// </summary>
        internal void Send(Packet packet, bool returnPacketToPool = true)
        {
            var (packetsSent, bytesSent) = ExecuteSend(packet, returnPacketToPool);
            PacketsSent += packetsSent;
            BytesSent += bytesSent;
        }

        /// <summary>
        /// Executes logic required to send the given packet.
        /// </summary>
        protected abstract (int packetsSent, int bytesSent) ExecuteSend(Packet packet, bool returnPacketToPool);

        /// <summary>
        /// Reads header information and attempts to convert incoming bytes to packet instance(s).
        /// </summary>
        internal void Receive(byte[] datagram, int bytesReceived)
        {
            PacketsReceived++;
            BytesReceived += bytesReceived;
            ExecuteReceive(datagram, bytesReceived);
        }

        /// <summary>
        /// Executes logic of receiving the incoming datagram.
        /// </summary>
        protected abstract void ExecuteReceive(byte[] datagram, int bytesReceived);

        /// <summary>
        /// Receives and processes acknowledgement packet. This method should be implemented by reliable channels,
        /// but is also useful as a diagnostic tool to write warnings if ack is received on the unreliable channel.
        /// </summary>
        internal abstract void ReceiveAcknowledgement(byte[] datagram);

        /// <summary>
        /// Returns true if first sequence number is greater than the second. This method
        /// takes into account sequence number overflow, meaning that comparing maximum
        /// sequence number and zero will properly deduce that zero is a greater sequence
        /// number as overflow occured.
        /// </summary>
        /// <param name="s1">First sequence number.</param>
        /// <param name="s2">Second sequence number.</param>
        protected static bool IsFirstSequenceNumberGreater(ushort s1, ushort s2) =>
            s1 > s2 && s1 - s2 <= ushort.MaxValue / 2 || s1 < s2 && s2 - s1 > ushort.MaxValue / 2;

        /// <summary>
        /// Returns statistics of this channel written in textual form.
        /// </summary>
        public override string ToString() =>
            $"{Name} | Sent: {PacketsSent}, {BytesSent} | Received: {PacketsReceived}, {BytesReceived}";
    }
}
