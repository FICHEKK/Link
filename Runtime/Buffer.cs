using System;
using System.Collections.Generic;

namespace Link
{
    /// <summary>
    /// Represents a reusable byte-array to which primitive data can be written to and read from.
    /// </summary>
    internal sealed class Buffer
    {
        /// <summary>
        /// Collection of reusable <see cref="Buffer"/> instances used to avoid frequent memory allocations.
        /// </summary>
        private static readonly Queue<Buffer> BufferPool = new();
        
        /// <summary>
        /// Default size of a newly created <see cref="Buffer"/> instance.
        /// </summary>
        public static int DefaultSize { get; set; } = 1024;

        /// <summary>
        /// Represents total number of new buffer allocations. This value should eventually stagnate if
        /// buffers are properly returned (unless big buffers are created frequently, which will not be
        /// returned to preserve memory). If this value keeps on increasing, that is a clear sign that
        /// there is a buffer leak - somewhere a buffer is taken but not returned to the pool.
        /// </summary>
        public static int AllocationCount { get; private set; }

        /// <summary>
        /// Gets the direct reference to the underlying byte-array.
        /// </summary>
        public byte[] Bytes => _isInPool ? throw new InvalidOperationException("Cannot get bytes of a buffer that is in pool.") : _bytes;
        
        /// <summary>
        /// Returns the number of bytes currently written to this buffer.
        /// </summary>
        public int Size { get; private set; }

        /// <summary>
        /// Underlying byte-array used by this <see cref="Buffer"/> instance.
        /// </summary>
        private readonly byte[] _bytes;

        /// <summary>
        /// Indicates whether this instance is currently stored in the <see cref="BufferPool"/>.
        /// It is used to ensure that the same instance is not returned to the pool more than once
        /// as well as ensuring that instance in pool is not accidentally used.
        /// </summary>
        private bool _isInPool;
        
        public static Buffer Copy(Buffer buffer)
        {
            var copy = Get();
            Array.Copy(buffer.Bytes, copy.Bytes, buffer.Size);
            copy.Size = buffer.Size;
            return copy;
        }
        
        public static Buffer From(byte[] bytes, int size)
        {
            var buffer = Get();
            Array.Copy(bytes, buffer.Bytes, size);
            buffer.Size = size;
            return buffer;
        }
        
        /// <summary>
        /// Returns a <see cref="Buffer"/> instance from the pool (or creates new if pool is empty).
        /// </summary>
        public static Buffer Get()
        {
            lock (BufferPool)
            {
                if (BufferPool.Count > 0)
                {
                    var buffer = BufferPool.Dequeue();
                    buffer.Size = 0;
                    buffer._isInPool = false;
                    return buffer;
                }
            }

            AllocationCount++;
            return new Buffer(DefaultSize);
        }

        /// <summary>
        /// Creates a new instance with the specified size.
        /// </summary>
        private Buffer(int size) => _bytes = new byte[size];

        public unsafe void Write<T>(T value) where T : unmanaged
        {
            if (Size + sizeof(T) > Bytes.Length)
                throw new InvalidOperationException($"Not enough space to write '{typeof(T)}'.");

            Write(value, Size);
            Size += sizeof(T);
        }

        public unsafe void Write<T>(T value, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &Bytes[offset])
            {
                *(T*) pointer = value;
            }
        }

        public void WriteArray<T>(T[] array) where T : unmanaged =>
            WriteArray(array, start: 0, array.Length);
        
        public unsafe void WriteArray<T>(T[] array, int start, int length) where T : unmanaged
        {
            if (Size + length * sizeof(T) > Bytes.Length)
                throw new InvalidOperationException($"Not enough space to write '{typeof(T)}' array.");
            
            WriteArray(array, start, length, Size);
            Size += length * sizeof(T);
        }
        
        public void WriteArray<T>(T[] array, int offset) where T : unmanaged =>
            WriteArray(array, start: 0, array.Length, offset);

        public unsafe void WriteArray<T>(T[] array, int start, int length, int offset) where T : unmanaged
        {
            fixed (byte* pointer = &Bytes[offset])
            {
                var typedPointer = (T*) pointer;

                for (int i = start, end = start + length; i < end; i++)
                {
                    *typedPointer = array[i];
                    typedPointer++;
                }
            }
        }
        
        public void WriteVarInt(int value)
        {
            var bytesNeeded = VarIntBytesNeeded(value);
            
            if (Size + bytesNeeded > Bytes.Length)
                throw new InvalidOperationException($"Not enough space to write var-int of value {value}.");

            WriteVarInt(value, Size);
            Size += bytesNeeded;
        }
        
        /// <summary>
        /// Returns the number of bytes needed to encode given integer using variable-length-encoding.
        /// </summary>
        public static int VarIntBytesNeeded(int value) => value switch
        {
            // Every negative value has most significant bit set,
            // therefore it requires maximum number of bytes.
            < 0 => 5,
            < 128 => 1,
            < 16_384 => 2,
            < 2_097_152 => 3,
            < 268_435_456 => 4,
            _ => 5
        };
        
        public void WriteVarInt(int value, int offset)
        {
            var v = (uint) value;

            while (v >= 0x80)
            {
                _bytes[offset++] = (byte) (v | 0x80);
                v >>= 7;
            }

            _bytes[offset] = (byte) v;
        }

        public unsafe T Read<T>(int offset) where T : unmanaged
        {
            fixed (byte* pointer = &Bytes[offset])
            {
                return *(T*) pointer;
            }
        }

        public unsafe T[] ReadArray<T>(int length, int offset) where T : unmanaged
        {
            var array = new T[length];
            
            fixed (byte* pointer = &Bytes[offset])
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

        public int ReadVarInt(int offset, out int bytesRead)
        {
            var value = 0;
            var shift = 0;
            bytesRead = 0;

            do
            {
                if (offset >= _bytes.Length)
                    throw new InvalidOperationException("Could not read var-int (out-of-bounds bytes).");
                
                if (shift == 5 * 7)
                    throw new InvalidOperationException("Invalid var-int format (requires more than 5 bytes).");

                value |= (_bytes[offset] & 0x7F) << shift;
                shift += 7;
                bytesRead++;
            } while ((_bytes[offset++] & 0x80) != 0);

            return value;
        }

        /// <summary>
        /// Returns this instance to the pool unless it is already in the pool.
        /// </summary>
        /// <returns><c>true</c> if instance was successfully returned to the pool, <c>false</c> otherwise.</returns>
        public bool Return()
        {
            lock (BufferPool)
            {
                if (_isInPool)
                {
                    Log.Error($"Attempted to return '{nameof(Buffer)}' instance that is already in pool.");
                    return false;
                }

                BufferPool.Enqueue(this);
                _isInPool = true;
                return true;
            }
        }
        
        ~Buffer()
        {
            // If buffer is in the pool, it was properly returned.
            if (_isInPool) return;
            
            // Otherwise, buffer leak occured.
            Log.Warning("Buffer leak occured (it wasn't properly returned to the pool).");
        }
    }
}
