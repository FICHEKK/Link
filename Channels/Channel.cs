namespace Networking.Transport.Channels
{
    /// <summary>
    /// Defines the way data is sent and received.
    /// </summary>
    internal abstract class Channel
    {
        /// <summary>
        /// Writes header information and sends given packet to the remote end-point.
        /// </summary>
        internal abstract void Send(Packet packet, bool returnPacketToPool = true);

        /// <summary>
        /// Reads header information and attempts to convert incoming bytes to packet instance(s).
        /// </summary>
        internal abstract void Receive(byte[] datagram, int bytesReceived);

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
    }
}
