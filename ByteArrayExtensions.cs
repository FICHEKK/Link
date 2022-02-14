namespace Networking.Transport
{
    /// <summary>
    /// Component that extends byte array with the ability to read and write primitive values.
    /// </summary>
    public static class ByteArrayExtensions
    {
        public static unsafe void Write<T>(this byte[] bytes, T value, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &bytes[offset])
            {
                *(T*) pointer = value;
            }
        }

        public static unsafe T Read<T>(this byte[] bytes, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &bytes[offset])
            {
                return *(T*) pointer;
            }
        }
    }
}
