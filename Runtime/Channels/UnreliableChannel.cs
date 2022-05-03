namespace Link.Channels
{
    public class UnreliableChannel : Channel
    {
        private readonly Connection _connection;

        public UnreliableChannel(Connection connection) =>
            _connection = connection;

        protected override (int packetsSent, int bytesSent) SendData(Packet packet) =>
            _connection.Node.Send(packet, _connection.RemoteEndPoint) ? (1, packet.Size) : (0, 0);

        protected override void ReceiveData(ReadOnlyPacket packet) =>
            _connection.Node.Receive(packet, _connection.RemoteEndPoint);

        internal override void ReceiveAcknowledgement(ReadOnlyPacket packet) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(UnreliableChannel)}'.");
    }
}
