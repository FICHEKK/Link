using System;

namespace Link
{
    /// <summary>
    /// Component that allows user to easily read data from the packet.
    /// It can be allocated only on stack to ensure that it is not cached.
    /// </summary>
    public ref struct PacketReader
    {
        /// <summary>
        /// Returns how many more bytes can be read from the packet.
        /// </summary>
        public int BytesLeft => _packet.Size - _readPosition;
        
        private readonly Packet _packet;
        private int _readPosition;

        /// <summary>
        /// Creates a new reader for the given packet, which starts reading at the specified position.
        /// </summary>
        public PacketReader(Packet packet, int readPosition = 0)
        {
            _packet = packet;
            _readPosition = readPosition;
        }
        
        /// <summary>
        /// Reads a <see cref="string"/> using encoding defined by <see cref="Packet.Encoding"/>.
        /// </summary>
        public string ReadString()
        {
            var stringByteCount = Read<int>();
            
            if (BytesLeft < stringByteCount)
                throw new InvalidOperationException("Could not read string (out-of-bounds bytes).");
            
            var stringValue = Packet.Encoding.GetString(_packet.Buffer, _readPosition, stringByteCount);
            _readPosition += stringByteCount;
            return stringValue;
        }

        /// <summary>
        /// Reads a value of specified type from the packet.
        /// </summary>
        public unsafe T Read<T>() where T : unmanaged
        {
            if (BytesLeft < sizeof(T))
                throw new InvalidOperationException($"Could not read value of type '{typeof(T)}' (out-of-bounds bytes).");
            
            var value = _packet.Buffer.Read<T>(_readPosition);
            _readPosition += sizeof(T);
            return value;
        }

        /// <summary>
        /// Reads an array of values of specified type from the packet.
        /// This method first reads number of elements, then calls <see cref="ReadSlice{T}"/>.
        /// </summary>
        public T[] ReadArray<T>() where T : unmanaged
        {
            var length = Read<int>();
            return ReadSlice<T>(length);
        }
        
        /// <summary>
        /// Reads a slice of values of specified type from the packet.
        /// This method simply reads specified number of elements from the packet.
        /// </summary>
        public unsafe T[] ReadSlice<T>(int length) where T : unmanaged
        {
            if (length == 0)
                return Array.Empty<T>();
            
            if (length < 0)
                throw new InvalidOperationException($"Cannot read slice of length {length} as it is negative.");

            if (length * sizeof(T) < 0)
                throw new InvalidOperationException($"Cannot read slice of length {length} as it is too big.");
            
            if (BytesLeft < length * sizeof(T))
                throw new InvalidOperationException($"Could not read slice of type '{typeof(T)}' (out-of-bounds bytes).");

            var slice = _packet.Buffer.ReadSlice<T>(length, _readPosition);
            _readPosition += length * sizeof(T);
            return slice;
        }
    }
}
