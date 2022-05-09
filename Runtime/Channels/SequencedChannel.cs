namespace Link.Channels
{
    public class SequencedChannel : Channel
    {
        public long PacketsReceivedOutOfOrder { get; private set; }
        public long BytesReceivedOutOfOrder { get; private set; }
        
        private readonly Connection _connection;
        private readonly object _sendLock = new();
        private readonly object _receiveLock = new();
        
        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;

        public SequencedChannel(Connection connection) =>
            _connection = connection;

        protected override void SendData(Packet packet)
        {
            lock (_sendLock)
            {
                packet.Write(++_localSequenceNumber);
                _connection.Node.Send(packet, _connection.RemoteEndPoint);
            }
        }

        protected override void ReceiveData(ReadOnlyPacket packet)
        {
            lock (_receiveLock)
            {
                var sequenceNumber = packet.Read<ushort>(position: packet.Size - sizeof(ushort));

                if (!IsFirstSequenceNumberGreater(sequenceNumber, _remoteSequenceNumber))
                {
                    PacketsReceivedOutOfOrder++;
                    BytesReceivedOutOfOrder += packet.Size;
                    return;
                }

                _remoteSequenceNumber = sequenceNumber;
                _connection.Node.Receive(packet, _connection.RemoteEndPoint);
            }
        }

        internal override void ReceiveAcknowledgement(ReadOnlyPacket packet) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(SequencedChannel)}'.");

        public override string ToString() =>
            base.ToString() + $" | Received out-of-order: {PacketsReceivedOutOfOrder}, {BytesReceivedOutOfOrder}";
    }
}
