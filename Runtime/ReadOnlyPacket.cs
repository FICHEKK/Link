using System;
using Link.Serialization;

namespace Link
{
    /// <summary>
    /// Represents a read-only packet view.
    /// </summary>
    public readonly ref struct ReadOnlyPacket
    {
        /// <summary>
        /// Returns identifier of the channel on which this packet was received on.
        /// </summary>
        public byte ChannelId => Buffer.Bytes[1];

        /// <summary>
        /// Returns identifier of the packet.
        /// </summary>
        public ushort Id => (ushort) Buffer.ReadShort(offset: 2);
        
        /// <summary>
        /// Returns the number of bytes contained in this packet.
        /// </summary>
        public int Size => Buffer.Size - Packet.HeaderSize;

        /// <summary>
        /// Returns how many more bytes can be read from this packet. Attempting to
        /// read more is going to result in the exception being thrown.
        /// </summary>
        public int UnreadBytes => Buffer.Size - Buffer.ReadPosition;
        
        /// <summary>
        /// Underlying packet from which this read-only view is reading from.
        /// </summary>
        internal Buffer Buffer { get; }

        /// <summary>
        /// Creates a new read-only view of the given packet, which starts reading at the specified position.
        /// </summary>
        internal ReadOnlyPacket(Buffer buffer, int start = 0)
        {
            Buffer = buffer;
            Buffer.ReadPosition = start;
        }
        
        /// <summary>
        /// Gets a byte at the specified index. Index must be in range from 0 (inclusive) to <see cref="Size"/> (exclusive).
        /// </summary>
        public byte this[int index]
        {
            get
            {
                if (index < 0)
                    throw new InvalidOperationException($"Cannot get byte at index {index}.");
                
                if (index >= Size)
                    throw new InvalidOperationException($"Cannot get byte at index {index} as it is equal to or exceeds packet size of {Size}.");
                
                return Buffer.Bytes[Packet.HeaderSize + index];
            }
        }

        /// <summary>
        /// Reads a value of specified type from the packet.
        /// </summary>
        public T Read<T>() => Serializers.Get<T>().Read(this);
    }
}
