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
        /// Maximum allowed size of a pooled buffer. Trying to return a bigger buffer to the pool is going
        /// to result in rejection by the pool. This is needed as a measure to prevent allocating too much
        /// memory, which would happen if there we too many big buffers stored in the pool.
        /// </summary>
        public static int MaxSize { get; set; } = ushort.MaxValue;

        /// <summary>
        /// Represents total number of new buffer allocations. This value should eventually stagnate if
        /// buffers are properly returned (unless big buffers are created frequently, which will not be
        /// returned to preserve memory). If this value keeps on increasing, that is a clear sign that
        /// there is a buffer leak - somewhere a buffer is taken but not returned to the pool.
        /// </summary>
        public static int AllocationCount { get; private set; }

        /// <summary>
        /// Gets or set the direct reference to the underlying byte-array.
        /// </summary>
        public byte[] Bytes { get; private set; }
        
        /// <summary>
        /// Returns the number of bytes currently written to this buffer.
        /// </summary>
        public int Size { get; private set; }

        /// <summary>
        /// Indicates whether this instance is currently stored in the <see cref="BufferPool"/>.
        /// It is used to ensure that the same instance is not returned to the pool more than once.
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
        
        public static Buffer With(byte[] bytes)
        {
            var buffer = Get();
            buffer.Bytes = bytes;
            buffer.Size = bytes.Length;
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
        private Buffer(int size) => Bytes = new byte[size];

        public void WriteArray<T>(T[] array) where T : unmanaged =>
            WriteArray(array, start: 0, array.Length);
        
        public void WriteArray<T>(T[] array, int offset) where T : unmanaged =>
            WriteArray(array, start: 0, array.Length, offset);

        public unsafe void WriteArray<T>(T[] array, int start, int length) where T : unmanaged
        {
            EnsureBufferSize(Size + length * sizeof(T));
            WriteArray(array, start, length, Size);
            Size += length * sizeof(T);
        }
        
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
        
        public unsafe void Write<T>(T value) where T : unmanaged
        {
            EnsureBufferSize(Size + sizeof(T));
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

        private void EnsureBufferSize(int requiredBufferSize)
        {
            if (Bytes.Length >= requiredBufferSize) return;

            var expandedBuffer = new byte[Math.Max(Bytes.Length * 2, requiredBufferSize)];
            Array.Copy(Bytes, expandedBuffer, Size);
            Bytes = expandedBuffer;
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

        /// <summary>
        /// Returns this instance to the pool unless it is already in the pool or its size exceeds <see cref="MaxSize"/> bytes.
        /// </summary>
        /// <returns><c>true</c> if instance was successfully returned to the pool, <c>false</c> otherwise.</returns>
        public bool Return()
        {
            lock (BufferPool)
            {
                if (_isInPool)
                {
                    Log.Error($"Attempt was made to return a '{nameof(Buffer)}' instance that is already in pool.");
                    return false;
                }

                if (Bytes.Length > MaxSize)
                {
                    Log.Info($"Big '{nameof(Buffer)}' instance ({Bytes.Length} bytes) was rejected by the pool to preserve memory.");
                    return false;
                }

                BufferPool.Enqueue(this);
                _isInPool = true;
                return true;
            }
        }
    }
}
