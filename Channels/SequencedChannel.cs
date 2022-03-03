namespace Networking.Transport.Channels
{
    /// <summary>
    /// Sequence number is attached to each packet. Packets can be lost, but won't be duplicated and will preserve order.
    /// When packet is received, it is processed in the following manner: If received sequence number is greater than last
    /// received sequence number, packet is processed (and last received sequence number is set to received), otherwise it
    /// is discarded. Perfect for rapidly changing state where only the latest state is important.
    /// </summary>
    public class SequencedChannel : Channel
    {
        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;

        public override byte Id => (byte) HeaderType.SequencedData;
        public override int HeaderSizeInBytes => 3;

        internal override void PreparePacketForSending(Packet packet)
        {
            _localSequenceNumber++;
            packet.Buffer.Write(_localSequenceNumber, offset: 1);
        }

        internal override Packet PreparePacketForHandling(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            if (!IsFirstSequenceNumberGreater(sequenceNumber, _remoteSequenceNumber)) return null;

            _remoteSequenceNumber = sequenceNumber;
            return ConvertDatagramToPacket(datagram, bytesReceived);
        }
    }
}
