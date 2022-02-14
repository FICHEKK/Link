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

        public static unsafe void WriteArray<T>(this byte[] bytes, T[] array, int offset) where T : unmanaged
        {
            bytes.Write(array.Length, offset);
            offset += sizeof(int);

            fixed (byte* pointer = &bytes[offset])
            {
                var typedPointer = (T*) pointer;

                foreach (var value in array)
                {
                    *typedPointer = value;
                    typedPointer++;
                }
            }
        }

        public static unsafe T Read<T>(this byte[] bytes, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &bytes[offset])
            {
                return *(T*) pointer;
            }
        }

        public static unsafe T[] ReadArray<T>(this byte[] bytes, int offset) where T : unmanaged
        {
            var length = bytes.Read<int>(offset);
            offset += sizeof(int);

            var array = new T[length];

            fixed (byte* pointer = &bytes[offset])
            {
                var typedPointer = (T*) pointer;

                for (var i = 0; i < length; i++)
                {
                    array[i] = *typedPointer;
                    typedPointer++;
                }
            }

            return array;
        }
    }
}
