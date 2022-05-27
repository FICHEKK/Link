namespace Link.Channels
{
    public class UnreliableChannel : Channel
    {
        private readonly Connection _connection;

        public UnreliableChannel(Connection connection) =>
            _connection = connection;

        protected override void SendData(Packet packet) =>
            _connection.Node.Send(packet, _connection.RemoteEndPoint);

        protected override void ReceiveData(ReadOnlyPacket packet) =>
            _connection.Node.Receive(packet.Buffer, _connection.RemoteEndPoint);

        internal override void ReceiveAcknowledgement(ReadOnlyPacket packet) =>
            Log.Warning($"Acknowledgement packet received on channel '{Name}'.");
    }
}
