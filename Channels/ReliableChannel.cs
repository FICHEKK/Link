namespace Networking.Transport.Channels
{
    /// <summary>
    /// Represents a channel that keeps resending packets until they are either
    /// acknowledged or deemed lost as a result of bad network conditions.
    /// </summary>
    public abstract class ReliableChannel : Channel
    {
        /// <summary>
        /// Maximum number of resend attempts before deeming the packet as lost.
        /// </summary>
        public int MaxResendAttempts { get; set; } = 15;

        /// <summary>
        /// Minimum possible time duration before resending the packet, in milliseconds.
        /// </summary>
        public int MinResendDelay { get; set; } = 100;

        /// <summary>
        /// Time between each consecutive resend is going to get increased by this factor.
        /// Sometimes connection can have a sudden burst of packet loss and trying to
        /// rapidly resend packets is not going to ensure it gets thorough. Waiting for
        /// more and more time gives connection time to stabilize itself.
        /// </summary>
        public double BackoffFactor { get; set; } = 1.2;

        /// <summary>
        /// Returns the most recently calculated round-trip time, in milliseconds.
        /// </summary>
        public double RoundTripTime => Connection.RoundTripTime;

        /// <summary>
        /// Returns packet loss percentage (value from 0 to 1) that occured on this channel.
        /// </summary>
        public double PacketLoss => PacketsResent > 0 ? (double) PacketsResent / (PacketsSent + PacketsResent) : 0;

        /// <summary>
        /// Total number of packets resent through this channel.
        /// </summary>
        public long PacketsResent { get; private set; }

        /// <summary>
        /// Total number of bytes resent through this channel.
        /// </summary>
        public long BytesResent { get; private set; }

        /// <summary>
        /// Total number of duplicate packets received on this channel.
        /// </summary>
        public long PacketsDuplicated { get; protected set; }

        /// <summary>
        /// Total number of duplicate bytes received on this channel.
        /// </summary>
        public long BytesDuplicated { get; protected set; }

        /// <summary>
        /// Connection that is using this channel.
        /// </summary>
        protected readonly Connection Connection;

        /// <summary>
        /// Constructs a new reliable channel for the given connection.
        /// </summary>
        protected ReliableChannel(Connection connection) => Connection = connection;

        /// <summary>
        /// Retries sending packet as acknowledgement wasn't received in time.
        /// </summary>
        /// <param name="packet">Packet being resent.</param>
        public void ResendPacket(Packet packet)
        {
            Connection.Node.Send(packet, Connection.RemoteEndPoint);
            Log.Info($"Re-sent packet {ExtractPacketInfo(packet)}.");

            PacketsResent++;
            BytesResent += packet.Writer.Position;
        }

        /// <summary>
        /// Handles the case of packet exceeding maximum resend attempts.
        /// </summary>
        /// <param name="packet">Packet that was lost.</param>
        public void HandleLostPacket(Packet packet)
        {
            Connection.Timeout();
            Log.Info($"Connection timed-out: Packet {ExtractPacketInfo(packet)} exceeded maximum resend attempts of {MaxResendAttempts}.");
        }

        /// <summary>
        /// Returns information stored inside the given packet.
        /// </summary>
        protected abstract string ExtractPacketInfo(Packet packet);

        /// <inheritdoc cref="Channel.ToString"/>
        public override string ToString() =>
            base.ToString() + $" | Duplicated: {PacketsDuplicated}, {BytesDuplicated} | Resent: {PacketsResent}, {BytesResent} | Packet-loss: {PacketLoss:F3}";
    }
}
