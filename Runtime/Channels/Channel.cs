namespace Link.Channels
{
    /// <summary>
    /// Defines the way data is sent and received.
    /// </summary>
    public abstract class Channel
    {
        /// <summary>
        /// Every packet that goes through any channel has the same header
        /// which consists of header type (1 byte) and channel ID (1 byte).
        /// </summary>
        protected const int HeaderSize = 2;

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
        /// Processes and sends given data-packet to the remote end-point.
        /// </summary>
        internal void Send(Packet packet)
        {
            var (packetsSent, bytesSent) = SendData(packet);
            PacketsSent += packetsSent;
            BytesSent += bytesSent;
        }

        /// <summary>
        /// Processes and sends outgoing data-packet.
        /// </summary>
        protected abstract (int packetsSent, int bytesSent) SendData(Packet packet);

        /// <summary>
        /// Receives and processes incoming data-packet.
        /// </summary>
        internal void Receive(PacketReader reader)
        {
            PacketsReceived++;
            BytesReceived += reader.Size;
            ReceiveData(reader);
        }

        /// <summary>
        /// Receives and processes incoming data-packet.
        /// </summary>
        protected abstract void ReceiveData(PacketReader reader);

        /// <summary>
        /// Receives and processes acknowledgement packet. This method should be implemented by reliable channels,
        /// but is also useful as a diagnostic tool to write warnings if ack is received on the unreliable channel.
        /// </summary>
        internal abstract void ReceiveAcknowledgement(PacketReader reader);

        /// <summary>
        /// Returns statistics of this channel written in textual form.
        /// </summary>
        public override string ToString() =>
            $"{Name} | Sent: {PacketsSent}, {BytesSent} | Received: {PacketsReceived}, {BytesReceived}";

        /// <summary>
        /// Returns true if first sequence number is greater than the second. This method
        /// takes into account sequence number overflow, meaning that comparing maximum
        /// sequence number and zero will properly deduce that zero is a greater sequence
        /// number as overflow occured.
        /// </summary>
        /// <param name="s1">First sequence number.</param>
        /// <param name="s2">Second sequence number.</param>
        internal static bool IsFirstSequenceNumberGreater(ushort s1, ushort s2) =>
            s1 > s2 && s1 - s2 <= ushort.MaxValue / 2 || s1 < s2 && s2 - s1 > ushort.MaxValue / 2;
    }
}
