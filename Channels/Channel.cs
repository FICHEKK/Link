using System;

namespace Networking.Transport.Channels
{
    /// <summary>
    /// Defines the way data is sent and received.
    /// </summary>
    public abstract class Channel
    {
        /// <inheritdoc cref="UnreliableChannel"/>
        public static readonly Channel Unreliable = new UnreliableChannel();

        /// <inheritdoc cref="SequencedChannel"/>
        public static readonly Channel Sequenced = new SequencedChannel();

        /// <inheritdoc cref="ReliableChannel"/>
        public static readonly Channel Reliable = new ReliableChannel();

        /// <summary>
        /// Uniquely identifies this channel.
        /// </summary>
        public abstract byte Id { get; }

        /// <summary>
        /// Defines how many bytes needed to store header information.
        /// </summary>
        public abstract int HeaderSizeInBytes { get; }

        /// <summary>
        /// Writes required header information to the given outgoing packet.
        /// </summary>
        internal abstract void PreparePacketForSending(Packet packet);

        /// <summary>
        /// Attempts to convert incoming datagram bytes into a packet instance.
        /// </summary>
        internal abstract Packet PreparePacketForHandling(byte[] datagram, int bytesReceived);

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
