using System;

namespace Link.Channels
{
    public class UnreliableChannel : Channel
    {
        private readonly Connection _connection;

        public UnreliableChannel(Connection connection) =>
            _connection = connection;

        protected override (int packetsSent, int bytesSent) ExecuteSend(Packet packet) =>
            _connection.Node.Send(packet, _connection.RemoteEndPoint) ? (1, packet.WritePosition) : (0, 0);

        protected override void ExecuteReceive(ReadOnlySpan<byte> datagram) =>
            _connection.Node.EnqueuePendingPacket(CreatePacket(datagram), _connection.RemoteEndPoint);

        internal override void ReceiveAcknowledgement(ReadOnlySpan<byte> datagram) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(UnreliableChannel)}'.");
    }
}
