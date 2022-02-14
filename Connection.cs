using System;
using System.Net;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a connection between two end-points. It is a higher level class that
    /// internally handles packet delivery methods and keeps track of packet statistics.
    /// </summary>
    public class Connection
    {
        private const ushort MaxSequenceNumber = ushort.MaxValue;
        private const ushort HalfMaxSequenceNumber = MaxSequenceNumber / 2;

        public State CurrentState { get; internal set; }
        public EndPoint RemoteEndPoint { get; internal set; }

        private ushort _localSequenceNumber;
        private ushort _remoteSequenceNumber = MaxSequenceNumber;

        public void PreparePacketForSending(Packet packet)
        {
            if (packet.HeaderType == HeaderType.SequencedData)
                packet.Buffer.Write(_localSequenceNumber++, offset: 1);
        }

        public Packet PreparePacketForHandling(byte[] datagram, int bytesReceived)
        {
            var deliveryMethodId = datagram[0];

            if (deliveryMethodId == DeliveryMethod.Unreliable.Id)
                return ReceivePacket(datagram, bytesReceived, DeliveryMethod.Unreliable);

            if (deliveryMethodId == DeliveryMethod.Sequenced.Id)
                return ReceiveUnreliableSequencedPacket(datagram, bytesReceived);

            Log.Error($"Received datagram contains invalid first byte {deliveryMethodId}.");
            return null;
        }

        private Packet ReceiveUnreliableSequencedPacket(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);

            if (sequenceNumber == _remoteSequenceNumber)
                return null;

            if (IsFirstSequenceNumberGreater(_remoteSequenceNumber, sequenceNumber))
                return null;

            _remoteSequenceNumber = sequenceNumber;
            return ReceivePacket(datagram, bytesReceived, DeliveryMethod.Sequenced);
        }

        private static Packet ReceivePacket(byte[] datagram, int bytesReceived, DeliveryMethod deliveryMethod)
        {
            var packet = Packet.Get();
            Array.Copy(datagram, packet.Buffer, bytesReceived);

            // This is a packet that we receive, therefore only valid read position is important.
            packet.Reader.ReadPosition = deliveryMethod.HeaderSizeInBytes;
            return packet;
        }

        /// <summary>
        /// Returns true if first sequence number is greater than the second.
        /// </summary>
        /// <param name="s1">First sequence number.</param>
        /// <param name="s2">Second sequence number.</param>
        private static bool IsFirstSequenceNumberGreater(ushort s1, ushort s2) =>
            s1 > s2 && s1 - s2 <= HalfMaxSequenceNumber || s1 < s2 && s2 - s1 > HalfMaxSequenceNumber;

        /// <summary>
        /// Defines all of the states that a connection can be in.
        /// </summary>
        public enum State
        {
            Disconnected,
            Connecting,
            Connected
        }
    }
}
