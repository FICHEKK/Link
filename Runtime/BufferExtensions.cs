using System;

namespace Link
{
    /// <summary>
    /// Component that extends <see cref="byte"/> buffer with the ability to read and write structured data.
    /// </summary>
    internal static class BufferExtensions
    {
        /// <summary>
        /// Writes a single value to the specified position.
        /// </summary>
        public static unsafe void Write<T>(this byte[] buffer, T value, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &buffer[offset])
            {
                *(T*) pointer = value;
            }
        }

        /// <summary>
        /// Writes an array of values to the specified position.
        /// </summary>
        /// <remarks>Array length will be written before writing array values.</remarks>
        public static void WriteArray<T>(this byte[] buffer, T[] array, int offset) where T : unmanaged
        {
            buffer.Write(array.Length, offset);
            buffer.WriteSlice(array, start: 0, length: array.Length, offset + sizeof(int));
        }

        /// <summary>
        /// Writes a slice of an array to the specified position.
        /// </summary>
        public static unsafe void WriteSlice<T>(this byte[] buffer, T[] array, int start, int length, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &buffer[offset])
            {
                var typedPointer = (T*) pointer;

                for (int i = start, end = start + length; i < end; i++)
                {
                    *typedPointer = array[i];
                    typedPointer++;
                }
            }
        }

        /// <summary>
        /// Writes variable-length encoded integer (also known as "var-int") which uses
        /// significantly less memory for smaller values (which is very often the case).
        /// </summary>
        public static void WriteVarInt(this byte[] buffer, int value, int offset, out int bytesWritten)
        {
            var v = (uint) value;
            bytesWritten = 1;

            while (v >= 0x80)
            {
                buffer[offset++] = (byte) (v | 0x80);
                v >>= 7;
                bytesWritten++;
            }

            buffer[offset] = (byte) v;
        }

        /// <summary>
        /// Reads a single value from the specified position.
        /// </summary>
        public static unsafe T Read<T>(this byte[] buffer, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &buffer[offset])
            {
                return *(T*) pointer;
            }
        }

        /// <summary>
        /// Reads an array of values from the specified position.
        /// </summary>
        public static T[] ReadArray<T>(this byte[] buffer, int offset) where T : unmanaged
        {
            var length = buffer.Read<int>(offset);
            return buffer.ReadSlice<T>(length, offset + sizeof(int));
        }

        /// <summary>
        /// Reads a slice of an array from the specified position.
        /// </summary>
        public static unsafe T[] ReadSlice<T>(this byte[] buffer, int length, int offset) where T : unmanaged
        {
            var slice = new T[length];
            
            fixed (byte* pointer = &buffer[offset])
            {
                var typedPointer = (T*) pointer;

                for (var i = 0; i < length; i++)
                {
                    slice[i] = *typedPointer;
                    typedPointer++;
                }
            }

            return slice;
        }

        /// <summary>
        /// Reads variable-length encoded integer that was written with <see cref="WriteVarInt"/>.
        /// </summary>
        public static int ReadVarInt(this byte[] buffer, int offset, out int bytesRead)
        {
            var value = 0;
            var shift = 0;
            bytesRead = 0;

            do
            {
                if (shift == 5 * 7)
                    throw new InvalidOperationException("Variable length encoded value has invalid format (requires more than 5 bytes).");

                value |= (buffer[offset] & 0x7F) << shift;
                shift += 7;
                bytesRead++;
            } while ((buffer[offset++] & 0x80) != 0);

            return value;
        }
    }
}
