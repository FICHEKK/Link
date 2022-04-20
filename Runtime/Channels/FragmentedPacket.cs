using System;
using System.Collections.Generic;

namespace Link.Channels
{
    /// <summary>
    /// Represents a packet that consists of one or more smaller, limited-sized pieces called <i>fragments</i>.
    /// </summary>
    public class FragmentedPacket
    {
        private readonly Dictionary<int, Packet> _fragments = new();
        private readonly int _headerSize;
        private readonly int _bodySize;
        private readonly int _footerSize;
        private int _lastFragmentNumber;

        /// <summary>
        /// Returns reassembled packet if all fragments have been received, <c>null</c> otherwise.
        /// </summary>
        public Packet ReassembledPacket { get; private set; }

        /// <summary>
        /// Constructs a new fragmented packet with defined fragment structure.
        /// </summary>
        /// <param name="headerSize">Number of bytes contained in a single fragment header.</param>
        /// <param name="bodySize">Maximum number of data-bytes contained in a single fragment body.</param>
        /// <param name="footerSize">Number of bytes contained in a single fragment footer.</param>
        public FragmentedPacket(int headerSize, int bodySize, int footerSize)
        {
            _headerSize = headerSize;
            _bodySize = bodySize;
            _footerSize = footerSize;
            _lastFragmentNumber = -1;
        }

        /// <summary>
        /// Adds a new fragment to this fragmented packet.
        /// </summary>
        /// <param name="fragment">Fragment to be added.</param>
        /// <param name="fragmentNumber">Fragment number which defines where this packet is located in the full packet.</param>
        /// <param name="isLastFragment">Whether or not this fragment is the last piece of the full packet.</param>
        /// <returns><c>true</c> if fragment was successfully added, <c>false</c> if it already exists.</returns>
        public bool Add(Packet fragment, int fragmentNumber, bool isLastFragment)
        {
            if (_fragments.ContainsKey(fragmentNumber))
            {
                fragment.Return();
                return false;
            }

            if (isLastFragment && (_lastFragmentNumber = fragmentNumber) == 0)
            {
                ReassembledPacket = fragment;
                return true;
            }

            _fragments.Add(fragmentNumber, fragment);
            if (_fragments.Count == _lastFragmentNumber + 1) Reassemble();

            return true;
        }

        private void Reassemble()
        {
            var lastFragmentByteCount = _fragments[_lastFragmentNumber].Size - _headerSize - _footerSize;

            ReassembledPacket = Packet.Get();
            ReassembledPacket.Buffer = new byte[_lastFragmentNumber * _bodySize + lastFragmentByteCount];

            // Copy data from all of the full fragments.
            for (var i = 0; i < _lastFragmentNumber; i++)
                Array.Copy(_fragments[i].Buffer, _headerSize, ReassembledPacket.Buffer, i * _bodySize, _bodySize);

            // Copy data from the last fragment.
            Array.Copy(_fragments[_lastFragmentNumber].Buffer, _headerSize, ReassembledPacket.Buffer, _lastFragmentNumber * _bodySize, lastFragmentByteCount);

            // Once we have a reassembled packet, we no longer need fragments.
            foreach (var fragment in _fragments.Values) fragment.Return();
        }
    }
}
