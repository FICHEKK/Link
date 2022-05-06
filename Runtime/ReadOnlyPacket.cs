using System;

namespace Link
{
    /// <summary>
    /// Represents a read-only packet view.
    /// </summary>
    public ref struct ReadOnlyPacket
    {
        /// <summary>
        /// Returns identifier of the channel on which this packet was received on.
        /// </summary>
        public byte ChannelId => Buffer.Bytes[1];
        
        /// <summary>
        /// Returns total number of bytes contained in the packet.
        /// </summary>
        public int Size => Buffer.Size;
        
        /// <summary>
        /// Underlying packet from which this read-only view is reading from.
        /// </summary>
        internal Buffer Buffer { get; }
        
        /// <summary>
        /// Index at which next read operation will be performed.
        /// </summary>
        private int _position;

        /// <summary>
        /// Creates a new read-only view of the given packet, which starts reading at the specified position.
        /// </summary>
        internal ReadOnlyPacket(Buffer buffer, int position = 0)
        {
            Buffer = buffer;
            _position = position;
        }
        
        /// <summary>
        /// Reads a <see cref="string"/> using encoding defined by <see cref="Link.Packet.Encoding"/>.
        /// </summary>
        public string ReadString()
        {
            var stringByteCount = Read<int>();
            
            if (Size - _position < stringByteCount)
                throw new InvalidOperationException("Could not read string (out-of-bounds bytes).");
            
            var stringValue = Packet.Encoding.GetString(Buffer.Bytes, _position, stringByteCount);
            _position += stringByteCount;
            return stringValue;
        }

        /// <summary>
        /// Reads a value of specified type from the packet.
        /// </summary>
        public unsafe T Read<T>() where T : unmanaged
        {
            if (Size - _position < sizeof(T))
                throw new InvalidOperationException($"Could not read value of type '{typeof(T)}' (out-of-bounds bytes).");
            
            var value = Buffer.Read<T>(_position);
            _position += sizeof(T);
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
            
            if (Size - _position < length * sizeof(T))
                throw new InvalidOperationException($"Could not read array of type '{typeof(T)}' (out-of-bounds bytes).");

            var array = Buffer.ReadArray<T>(length, _position);
            _position += length * sizeof(T);
            return array;
        }
        
        internal T Read<T>(int position) where T : unmanaged
        {
            var currentPosition = _position;
            _position = position;
            
            var value = Read<T>();
            _position = currentPosition;
            
            return value;
        }
    }
}
