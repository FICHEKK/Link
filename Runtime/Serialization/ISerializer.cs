namespace Link.Serialization
{
    /// <summary>
    /// Represents a component that can serialize and deserialize specific data type.
    /// </summary>
    public interface ISerializer<T>
    {
        /// <summary>
        /// Writes a value to the given <see cref="Packet"/> instance.
        /// </summary>
        public void Write(Packet packet, T value);

        /// <summary>
        /// Reads a value from the given <see cref="ReadOnlyPacket"/> instance.
        /// </summary>
        public T Read(ReadOnlyPacket packet);
    }
}
