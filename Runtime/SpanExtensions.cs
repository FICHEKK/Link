using System;

namespace Link
{
    /// <summary>
    /// Component that extends <see cref="System.Span{T}"/> with the ability to write and
    /// <see cref="System.ReadOnlySpan{T}"/> with the ability to read primitive values.
    /// </summary>
    public static class SpanExtensions
    {
        /// <summary>
        /// Writes a single value to the specified position in this span.
        /// </summary>
        public static unsafe void Write<T>(this Span<byte> span, T value, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &span[offset])
            {
                *(T*) pointer = value;
            }
        }

        /// <summary>
        /// Writes an array of values to the specified position in this span.
        /// </summary>
        /// <remarks>Array length will be written before writing array values.</remarks>
        public static void WriteArray<T>(this Span<byte> span, T[] array, int offset) where T : unmanaged
        {
            span.Write(array.Length, offset);
            span.WriteSpan<T>(array.AsSpan(), offset + sizeof(int));
        }

        /// <summary>
        /// Writes a span of values to the specified position in this span.
        /// </summary>
        public static unsafe void WriteSpan<T>(this Span<byte> span, ReadOnlySpan<T> source, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &span[offset])
            {
                var typedPointer = (T*) pointer;

                foreach (var value in source)
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
        public static void WriteVarInt(this Span<byte> span, int value, int offset)
        {
            var v = (uint) value;

            while (v >= 0x80)
            {
                span[offset++] = (byte) (v | 0x80);
                v >>= 7;
            }

            span[offset] = (byte) v;
        }

        /// <summary>
        /// Reads a single value from the specified position in this span.
        /// </summary>
        public static unsafe T Read<T>(this ReadOnlySpan<byte> span, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &span[offset])
            {
                return *(T*) pointer;
            }
        }

        /// <summary>
        /// Reads an array of values from the specified position in this span.
        /// </summary>
        public static unsafe T[] ReadArray<T>(this ReadOnlySpan<byte> span, int offset) where T : unmanaged
        {
            var length = Read<int>(span, offset);
            offset += sizeof(int);

            var array = new T[length];

            fixed (byte* pointer = &span[offset])
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
        public static int ReadVarInt(this ReadOnlySpan<byte> span, int offset)
        {
            var value = 0;
            var shift = 0;

            do
            {
                if (shift == 5 * 7)
                    throw new InvalidOperationException("Variable length encoded value has invalid format (requires more than 5 bytes).");

                value |= (span[offset] & 0x7F) << shift;
                shift += 7;
            } while ((span[offset++] & 0x80) != 0);

            return value;
        }
    }
}
