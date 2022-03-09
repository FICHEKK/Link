using System.Net;
using Networking.Transport.Nodes;

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
        public override byte Id => 1;
        public override int HeaderSizeInBytes => 3;

        private readonly Node _node;
        private readonly EndPoint _remoteEndPoint;

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber;

        public SequencedChannel(Node node, EndPoint remoteEndPoint)
        {
            _node = node;
            _remoteEndPoint = remoteEndPoint;
        }

        internal override void Send(Packet packet, bool returnPacketToPool = true)
        {
            packet.Buffer.Write(++_localSequenceNumber, offset: 1);
            _node.Send(packet, _remoteEndPoint, returnPacketToPool);
        }

        internal override void Receive(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            if (!IsFirstSequenceNumberGreater(sequenceNumber, _remoteSequenceNumber)) return;

            _remoteSequenceNumber = sequenceNumber;
            _node.EnqueuePendingPacket(ConvertDatagramToPacket(datagram, bytesReceived), _remoteEndPoint);
        }

        internal override void ReceiveAcknowledgement(byte[] datagram) =>
            Log.Warning($"Acknowledgement packet received on '{nameof(SequencedChannel)}'.");
    }
}
