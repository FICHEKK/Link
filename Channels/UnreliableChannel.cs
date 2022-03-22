namespace Networking.Transport.Channels
{
    public class UnreliableChannel : Channel
    {
        private readonly Connection _connection;

        public UnreliableChannel(Connection connection) =>
            _connection = connection;

        protected override (int packetsSent, int bytesSent) ExecuteSend(Packet packet, bool returnPacketToPool) =>
            _connection.Node.Send(packet, _connection.RemoteEndPoint, returnPacketToPool) ? (1, packet.Writer.Position) : (0, 0);

        protected override void ExecuteReceive(byte[] datagram, int bytesReceived) =>
            _connection.Node.EnqueuePendingPacket(Packet.From(datagram, bytesReceived), _connection.RemoteEndPoint);

        internal override void ReceiveAcknowledgement(byte[] datagram) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(UnreliableChannel)}'.");
    }
}
