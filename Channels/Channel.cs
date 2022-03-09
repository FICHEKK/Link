using System;

namespace Networking.Transport.Channels
{
    /// <summary>
    /// Defines the way data is sent and received.
    /// </summary>
    public abstract class Channel
    {
        // TODO - Refactor this list of available channels into something more flexible.
        /// <inheritdoc cref="UnreliableChannel"/>
        public static readonly Channel Unreliable = new UnreliableChannel(null, null);

        /// <inheritdoc cref="SequencedChannel"/>
        public static readonly Channel Sequenced = new SequencedChannel(null, null);

        /// <inheritdoc cref="ReliableChannel"/>
        public static readonly Channel Reliable = new ReliableChannel(null, null);

        /// <summary>
        /// Uniquely identifies this channel. This value must not exceed
        /// 15 as it is used as the left nibble in the header byte.
        /// </summary>
        public abstract byte Id { get; }

        /// <summary>
        /// Defines how many bytes are needed to store header information.
        /// </summary>
        public abstract int HeaderSizeInBytes { get; }

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
        /// Converts given raw bytes to a packet instance.
        /// </summary>
        protected Packet ConvertDatagramToPacket(byte[] datagram, int bytesReceived)
        {
            var packet = Packet.Get();
            Array.Copy(datagram, packet.Buffer, bytesReceived);

            // This is a packet that we receive, therefore only valid read position is important.
            packet.Reader.ReadPosition = HeaderSizeInBytes;
            return packet;
        }

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
