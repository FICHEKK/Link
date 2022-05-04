using System;

namespace Link
{
    /// <summary>
    /// Represents a read-only packet view.
    /// </summary>
    public ref struct ReadOnlyPacket
    {
        /// <summary>
        /// Returns total number of bytes contained in the packet.
        /// </summary>
        public int Size => Buffer.Size;
        
        /// <summary>
        /// Gets or sets the index at which next read operation will be performed.
        /// </summary>
        public int Position { get; set; }
        
        /// <summary>
        /// Underlying packet from which this read-only view is reading from.
        /// </summary>
        internal Buffer Buffer { get; }

        /// <summary>
        /// Creates a new read-only view of the given packet, which starts reading at the specified position.
        /// </summary>
        internal ReadOnlyPacket(Buffer buffer, int position = 0)
        {
            Buffer = buffer;
            Position = position;
        }
        
        /// <summary>
        /// Reads a <see cref="string"/> using encoding defined by <see cref="Link.Packet.Encoding"/>.
        /// </summary>
        public string ReadString()
        {
            var stringByteCount = Read<int>();
            
            if (Size - Position < stringByteCount)
                throw new InvalidOperationException("Could not read string (out-of-bounds bytes).");
            
            var stringValue = Packet.Encoding.GetString(Buffer.Bytes, Position, stringByteCount);
            Position += stringByteCount;
            return stringValue;
        }

        /// <summary>
        /// Reads a value of specified type from the packet.
        /// </summary>
        public unsafe T Read<T>() where T : unmanaged
        {
            if (Size - Position < sizeof(T))
                throw new InvalidOperationException($"Could not read value of type '{typeof(T)}' (out-of-bounds bytes).");
            
            var value = Buffer.Read<T>(Position);
            Position += sizeof(T);
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
                length = Read<int>();

            if (length < 0)
                throw new InvalidOperationException($"Cannot read array of length {length} as it is negative.");

            if (length * sizeof(T) < 0)
                throw new InvalidOperationException($"Cannot read array of length {length} as it is too big.");
            
            if (Size - Position < length * sizeof(T))
                throw new InvalidOperationException($"Could not read array of type '{typeof(T)}' (out-of-bounds bytes).");

            var array = Buffer.ReadArray<T>(length, Position);
            Position += length * sizeof(T);
            return array;
        }
        
        internal T Read<T>(int position) where T : unmanaged
        {
            var currentPosition = Position;
            Position = position;
            
            var value = Read<T>();
            Position = currentPosition;
            
            return value;
        }
    }
}
