namespace Link.Channels
{
    public class SequencedChannel : Channel
    {
        public long PacketsReceivedOutOfOrder { get; private set; }
        public long BytesReceivedOutOfOrder { get; private set; }
        
        private readonly Connection _connection;
        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;

        public SequencedChannel(Connection connection) =>
            _connection = connection;

        protected override void SendData(Packet packet)
        {
            packet.Write(++_localSequenceNumber);
            _connection.Node.Send(packet, _connection.RemoteEndPoint);
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
