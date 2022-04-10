namespace Link.Channels
{
    public class SequencedChannel : Channel
    {
        private readonly Connection _connection;
        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;

        public long PacketsReceivedOutOfOrder { get; private set; }
        public long BytesReceivedOutOfOrder { get; private set; }

        public SequencedChannel(Connection connection) =>
            _connection = connection;

        protected override (int packetsSent, int bytesSent) ExecuteSend(Packet packet)
        {
            packet.Writer.Write(++_localSequenceNumber);
            return _connection.Node.Send(packet, _connection.RemoteEndPoint) ? (1, packet.Writer.Position) : (0, 0);
        }

        protected override void ExecuteReceive(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: bytesReceived - sizeof(ushort));

            if (IsFirstSequenceNumberGreater(sequenceNumber, _remoteSequenceNumber))
            {
                _remoteSequenceNumber = sequenceNumber;
                _connection.Node.EnqueuePendingPacket(CreatePacket(datagram, bytesReceived), _connection.RemoteEndPoint);
            }
            else
            {
                PacketsReceivedOutOfOrder++;
                BytesReceivedOutOfOrder += bytesReceived;
            }
        }

        internal override void ReceiveAcknowledgement(byte[] datagram) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(SequencedChannel)}'.");

        public override string ToString() =>
            base.ToString() + $" | Received out-of-order: {PacketsReceivedOutOfOrder}, {BytesReceivedOutOfOrder}";
    }
}
