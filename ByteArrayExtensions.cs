using System;

namespace Networking.Transport
{
    /// <summary>
    /// Component that extends byte array with the ability to read and write primitive values.
    /// </summary>
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Writes a single value to the specified position in this byte array.
        /// </summary>
        public static unsafe void Write<T>(this byte[] bytes, T value, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &bytes[offset])
            {
                *(T*) pointer = value;
            }
        }

        /// <summary>
        /// Writes an array of values to the specified position in this byte array.
        /// </summary>
        /// <remarks>Array length will be written before writing array values.</remarks>
        public static void WriteArray<T>(this byte[] bytes, T[] array, int offset) where T : unmanaged
        {
            bytes.Write(array.Length, offset);
            bytes.WriteSpan(new ReadOnlySpan<T>(array), offset + sizeof(int));
        }

        /// <summary>
        /// Writes a span of values to the specified position in this byte array.
        /// </summary>
        public static unsafe void WriteSpan<T>(this byte[] bytes, ReadOnlySpan<T> span, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &bytes[offset])
            {
                var typedPointer = (T*) pointer;

                foreach (var value in span)
                {
                    *typedPointer = value;
                    typedPointer++;
                }
            }
        }

        /// <summary>
        /// Writes variable-length encoded integer (also known as "var-int") which uses
        /// significantly less memory for smaller values (which is very often the case).
        /// </summary>
        public static void WriteVarInt(this byte[] bytes, int value, int offset)
        {
            var v = (uint) value;

            while (v >= 0x80)
            {
                bytes[offset++] = (byte) (v | 0x80);
                v >>= 7;
            }

            bytes[offset] = (byte) v;
        }

        /// <summary>
        /// Reads a single value from the specified position in this byte array.
        /// </summary>
        public static unsafe T Read<T>(this byte[] bytes, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &bytes[offset])
            {
                return *(T*) pointer;
            }
        }

        /// <summary>
        /// Reads an array of values from the specified position in this byte array.
        /// </summary>
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

        /// <summary>
        /// Reads variable-length encoded integer that was written with <see cref="WriteVarInt"/>.
        /// </summary>
        public static int ReadVarInt(this byte[] bytes, int offset)
        {
            var value = 0;
            var shift = 0;

            do
            {
                if (shift == 5 * 7)
                    throw new InvalidOperationException("Variable length encoded value has invalid format (requires more than 5 bytes).");

                value |= (bytes[offset] & 0x7F) << shift;
                shift += 7;
            } while ((bytes[offset++] & 0x80) != 0);

            return value;
        }
    }
}
