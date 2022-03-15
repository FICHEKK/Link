namespace Networking.Transport.Channels
{
    public class UnreliableChannel : Channel
    {
        private readonly Connection _connection;

        public UnreliableChannel(Connection connection) =>
            _connection = connection;

        internal override void Send(Packet packet, bool returnPacketToPool = true) =>
            _connection.Node.Send(packet, _connection.RemoteEndPoint, returnPacketToPool);

        internal override void Receive(byte[] datagram, int bytesReceived) =>
            _connection.Node.EnqueuePendingPacket(Packet.From(datagram, bytesReceived), _connection.RemoteEndPoint);

        internal override void ReceiveAcknowledgement(byte[] datagram) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(UnreliableChannel)}'.");
    }
}
