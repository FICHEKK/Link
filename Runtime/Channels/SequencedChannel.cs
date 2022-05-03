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

        protected override (int packetsSent, int bytesSent) SendData(Packet packet)
        {
            packet.Write(++_localSequenceNumber);
            return _connection.Node.Send(packet, _connection.RemoteEndPoint) ? (1, packet.Size) : (0, 0);
        }

        protected override void ReceiveData(ReadOnlyPacket packet)
        {
            var sequenceNumber = packet.Read<ushort>(position: packet.Size - sizeof(ushort));

            if (IsFirstSequenceNumberGreater(sequenceNumber, _remoteSequenceNumber))
            {
                _remoteSequenceNumber = sequenceNumber;
                _connection.Node.Receive(packet, _connection.RemoteEndPoint);
            }
            else
            {
                PacketsReceivedOutOfOrder++;
                BytesReceivedOutOfOrder += packet.Size;
            }
        }

        internal override void ReceiveAcknowledgement(ReadOnlyPacket packet) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(SequencedChannel)}'.");

        public override string ToString() =>
            base.ToString() + $" | Received out-of-order: {PacketsReceivedOutOfOrder}, {BytesReceivedOutOfOrder}";
    }
}
