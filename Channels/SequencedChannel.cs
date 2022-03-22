namespace Networking.Transport.Channels
{
    public class SequencedChannel : Channel
    {
        private readonly Connection _connection;
        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;

        public SequencedChannel(Connection connection) =>
            _connection = connection;

        protected override (int packetsSent, int bytesSent) ExecuteSend(Packet packet, bool returnPacketToPool)
        {
            packet.Buffer.Write(++_localSequenceNumber, offset: 1);
            return _connection.Node.Send(packet, _connection.RemoteEndPoint, returnPacketToPool) ? (1, packet.Writer.Position) : (0, 0);
        }

        protected override void ExecuteReceive(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            if (!IsFirstSequenceNumberGreater(sequenceNumber, _remoteSequenceNumber)) return;

            _remoteSequenceNumber = sequenceNumber;
            _connection.Node.EnqueuePendingPacket(Packet.From(datagram, bytesReceived), _connection.RemoteEndPoint);
        }

        internal override void ReceiveAcknowledgement(byte[] datagram) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(SequencedChannel)}'.");
    }
}
