namespace Link.Channels
{
    public class UnreliableChannel : Channel
    {
        private readonly Connection _connection;

        public UnreliableChannel(Connection connection) =>
            _connection = connection;

        protected override (int packetsSent, int bytesSent) SendData(Packet packet) =>
            _connection.Node.Send(packet, _connection.RemoteEndPoint) ? (1, packet.Size) : (0, 0);

        protected override void ReceiveData(byte[] datagram, int bytesReceived) =>
            _connection.Node.EnqueuePendingPacket(Packet.From(datagram, bytesReceived, HeaderSize), _connection.RemoteEndPoint);

        internal override void ReceiveAcknowledgement(byte[] datagram) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(UnreliableChannel)}'.");
    }
}
