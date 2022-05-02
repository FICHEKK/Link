namespace Link
{
    /// <summary>
    /// Component that allows user to easily write data to the packet.
    /// It can be allocated only on stack to ensure that it is not cached.
    /// </summary>
    public readonly ref struct PacketWriter
    {
        /// <inheritdoc cref="Link.Packet.Size"/>
        public int Size => Packet.Size;
        
        /// <summary>
        /// Underlying packet to which this writer is reading to.
        /// </summary>
        internal Packet Packet { get; }

        /// <summary>
        /// Creates a new writer which starts writing at position defined by <see cref="Link.Packet.Size"/>.
        /// </summary>
        internal PacketWriter(Packet packet) => Packet = packet;

        /// <summary>
        /// Writes a <see cref="string"/> using encoding defined by <see cref="Link.Packet.Encoding"/>.
        /// </summary>
        public PacketWriter Write(string value)
        {
            Packet.Write(value);
            return this;
        }

        /// <summary>
        /// Writes a value of specified type to the packet.
        /// </summary>
        public PacketWriter Write<T>(T value) where T : unmanaged
        {
            Packet.Write(value);
            return this;
        }

        /// <summary>
        /// Writes an array of values of specified type to the packet.
        /// This method first writes number of elements, then calls <see cref="WriteSlice{T}"/>.
        /// </summary>
        public PacketWriter WriteArray<T>(T[] array) where T : unmanaged
        {
            Packet.WriteArray(array);
            return this;
        }

        /// <summary>
        /// Writes a slice of values of specified type to the packet.
        /// This method simply writes specified range of elements to the packet.
        /// </summary>
        public PacketWriter WriteSlice<T>(T[] array, int start, int length) where T : unmanaged
        {
            Packet.WriteSlice(array, start, length);
            return this;
        }
    }
}
