using System;
using System.Collections.Generic;

namespace Networking.Transport.Channels
{
    public class FragmentedChannel : Channel, IReliableChannel
    {
        /// <summary>
        /// Maximum number of fragments a packet can consist of.
        /// </summary>
        private const int MaxFragmentCount = ushort.MaxValue >> 1;

        /// <summary>
        /// Defines how many data bytes can be stored in a single fragment.
        /// </summary>
        private const int BytesPerFragment = Packet.MaxSize - HeaderSize;

        /// <summary>
        /// Consists of header type (1 byte), sequence number (2 bytes) and fragment number (2 bytes).
        /// </summary>
        private const int HeaderSize = 5;

        /// <summary>
        /// When the most significant bit is set, it marks that fragment as the last fragment.
        /// </summary>
        private const int LastFragmentBitmask = 1 << 15;

        private readonly Dictionary<(ushort sequenceNumber, ushort fragmentNumber), PendingPacket> _pendingPackets = new();
        private readonly Dictionary<ushort, FragmentedPacket> _fragmentedPackets = new();
        private readonly Connection _connection;

        private ushort _sendSequenceNumber;
        private ushort _receiveSequenceNumber;

        public double RoundTripTime => _connection.RoundTripTime;

        public FragmentedChannel(Connection connection) =>
            _connection = connection;

        internal override void Send(Packet packet, bool returnPacketToPool = true)
        {
            var dataByteCount = packet.Writer.Position - HeaderSize;
            var fragmentCount = dataByteCount / BytesPerFragment + (dataByteCount % BytesPerFragment != 0 ? 1 : 0);

            if (fragmentCount > MaxFragmentCount)
            {
                Log.Error($"Packet is too large (consists of {fragmentCount} fragments, while maximum is {MaxFragmentCount} fragments).");
                if (returnPacketToPool) packet.Return();
            }

            lock (_pendingPackets)
            {
                for (var i = 0; i < fragmentCount; i++)
                {
                    var fragment = Packet.Get(HeaderType.Data, Delivery.Fragmented);
                    var fragmentNumber = i < fragmentCount - 1 ? (ushort) i : (ushort) (i | LastFragmentBitmask);
                    var fragmentLength = i < fragmentCount - 1 ? BytesPerFragment : dataByteCount - i * BytesPerFragment;

                    fragment.Writer.Write(_sendSequenceNumber);
                    fragment.Writer.Write(fragmentNumber);
                    fragment.Writer.WriteSpan(new ReadOnlySpan<byte>(packet.Buffer, start: HeaderSize + i * BytesPerFragment, fragmentLength));

                    _pendingPackets.Add((_sendSequenceNumber, fragmentNumber), PendingPacket.Get(fragment, reliableChannel: this));
                    _connection.Node.Send(fragment, _connection.RemoteEndPoint);
                }

                _sendSequenceNumber++;
            }
        }

        internal override void Receive(byte[] datagram, int bytesReceived)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            var fragmentNumber = datagram.Read<ushort>(offset: 3);
            SendAcknowledgement(sequenceNumber, fragmentNumber);

            if (!_fragmentedPackets.TryGetValue(sequenceNumber, out var fragmentedPacket))
            {
                fragmentedPacket = new FragmentedPacket();
                _fragmentedPackets.Add(sequenceNumber, fragmentedPacket);
            }

            fragmentedPacket.AddFragment(Packet.From(datagram, bytesReceived));

            while (true)
            {
                if (!_fragmentedPackets.TryGetValue(_receiveSequenceNumber, out var nextFragmentedPacket)) break;
                if (!nextFragmentedPacket.IsReassembled) break;

                _connection.Node.EnqueuePendingPacket(nextFragmentedPacket.ReassembledPacket, _connection.RemoteEndPoint);
                _receiveSequenceNumber++;
            }
        }

        // Executed on: Receive thread
        private void SendAcknowledgement(ushort sequenceNumber, ushort fragmentNumber)
        {
            var packet = Packet.Get(HeaderType.Acknowledgement, Delivery.Fragmented);
            packet.Writer.Write(sequenceNumber);
            packet.Writer.Write(fragmentNumber);
            _connection.Node.Send(packet, _connection.RemoteEndPoint);
        }

        // Executed on: Receive thread
        internal override void ReceiveAcknowledgement(byte[] datagram)
        {
            var sequenceNumber = datagram.Read<ushort>(offset: 1);
            var fragmentNumber = datagram.Read<ushort>(offset: 3);

            lock (_pendingPackets)
            {
                var key = (sequenceNumber, fragmentNumber);
                if (!_pendingPackets.TryGetValue(key, out var pendingPacket)) return;

                pendingPacket.Acknowledge();
                _pendingPackets.Remove(key);
            }
        }

        // Executed on: Worker thread
        public void ResendPacket(Packet packet)
        {
            _connection.Node.Send(packet, _connection.RemoteEndPoint, returnPacketToPool: false);

            var sequenceNumber = packet.Buffer.Read<ushort>(offset: 1);
            var fragmentNumber = packet.Buffer.Read<ushort>(offset: 3);
            Log.Info($"Re-sent packet [sequence: {sequenceNumber}, fragment: {fragmentNumber}].");
        }

        // Executed on: Worker thread
        public void HandleLostPacket(Packet packet)
        {
            _connection.Timeout();

            var sequenceNumber = packet.Buffer.Read<ushort>(offset: 1);
            var fragmentNumber = packet.Buffer.Read<ushort>(offset: 3);
            Log.Info($"Connection timed-out: Packet [sequence: {sequenceNumber}, fragment: {fragmentNumber}] exceeded maximum resend attempts.");
        }

        private class FragmentedPacket
        {
            public bool IsReassembled => ReassembledPacket is not null;
            public Packet ReassembledPacket { get; private set; }

            private readonly Dictionary<ushort, Packet> _fragments = new();
            private ushort _lastFragmentNumber;
            private int _totalFragmentCount;

            public void AddFragment(Packet fragment)
            {
                if (IsReassembled) return;

                var fragmentNumber = fragment.Buffer.Read<ushort>(offset: 3);
                if (_fragments.ContainsKey(fragmentNumber)) return;

                if ((fragmentNumber & LastFragmentBitmask) != 0)
                {
                    _lastFragmentNumber = fragmentNumber;
                    _totalFragmentCount = (fragmentNumber & ~LastFragmentBitmask) + 1;

                    if (_totalFragmentCount == 1)
                    {
                        ReassembledPacket = fragment;
                        return;
                    }
                }

                _fragments.Add(fragmentNumber, fragment);

                if (_fragments.Count == _totalFragmentCount) Reassemble();
            }

            private void Reassemble()
            {
                var fullFragmentByteCount = (_totalFragmentCount - 1) * BytesPerFragment;
                var lastFragmentByteCount = _fragments[_lastFragmentNumber].Writer.Position - HeaderSize;

                ReassembledPacket = Packet.Get();
                ReassembledPacket.Buffer = new byte[fullFragmentByteCount + lastFragmentByteCount];

                // Copy data from all of the full fragments.
                for (var i = 0; i < _totalFragmentCount - 1; i++)
                    Array.Copy(_fragments[(ushort) i].Buffer, HeaderSize, ReassembledPacket.Buffer, i * BytesPerFragment, BytesPerFragment);

                // Copy data from the last fragment.
                Array.Copy(_fragments[_lastFragmentNumber].Buffer, HeaderSize, ReassembledPacket.Buffer, (_totalFragmentCount - 1) * BytesPerFragment, lastFragmentByteCount);

                // Once we have a reassembled packet, we no longer need fragments.
                foreach (var fragment in _fragments.Values) fragment.Return();
            }
        }
    }
}
