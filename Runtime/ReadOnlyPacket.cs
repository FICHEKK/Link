using System;

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
        public ushort Id => Buffer.Read<ushort>(offset: 2);
        
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
        /// Reads a <see cref="string"/> using encoding defined by <see cref="Link.Packet.Encoding"/>.
        /// </summary>
        public string ReadString()
        {
            var stringByteCount = ReadVarInt();

            if (stringByteCount == 0)
                return string.Empty;
            
            if (UnreadBytes < stringByteCount)
                throw new InvalidOperationException("Could not read string (out-of-bounds bytes).");
            
            var stringValue = Packet.Encoding.GetString(Buffer.Bytes, Buffer.ReadPosition, stringByteCount);
            Buffer.ReadPosition += stringByteCount;
            return stringValue;
        }

        /// <summary>
        /// Reads a value of specified type from the packet.
        /// </summary>
        public unsafe T Read<T>() where T : unmanaged
        {
            if (UnreadBytes < sizeof(T))
                throw new InvalidOperationException($"Could not read value of type '{typeof(T)}' (out-of-bounds bytes).");
            
            var value = Buffer.Read<T>(Buffer.ReadPosition);
            Buffer.ReadPosition += sizeof(T);
            return value;
        }

        /// <summary>
        /// Reads an array of values of specified type from the packet.
        /// </summary>
        /// <param name="length">
        /// If set to 0 or greater, exactly that many elements will be read from the packet.
        /// If set to a negative value, length of the array will be read from the packet.
        /// </param>
        public unsafe T[] ReadArray<T>(int length = -1) where T : unmanaged
        {
            if (length == 0)
                return Array.Empty<T>();
            
            if (length < 0)
                length = ReadVarInt();

            if (length < 0)
                throw new InvalidOperationException($"Cannot read array of length {length} as it is negative.");

            if (length * sizeof(T) < 0)
                throw new InvalidOperationException($"Cannot read array of length {length} as it is too big.");
            
            if (UnreadBytes < length * sizeof(T))
                throw new InvalidOperationException($"Could not read array of type '{typeof(T)}' (out-of-bounds bytes).");

            var array = Buffer.ReadArray<T>(length, Buffer.ReadPosition);
            Buffer.ReadPosition += length * sizeof(T);
            return array;
        }

        private int ReadVarInt()
        {
            var varInt = Buffer.ReadVarInt(Buffer.ReadPosition, out var bytesRead);
            Buffer.ReadPosition += bytesRead;
            return varInt;
        }
    }
}
