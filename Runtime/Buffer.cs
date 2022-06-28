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
        /// Index at which next read operation will be performed.
        /// </summary>
        public int ReadPosition { get; set; }
        
        /// <summary>
        /// Returns the number of bytes that can be written to this buffer.
        /// </summary>
        public int Capacity => _bytes.Length;

        /// <summary>
        /// Underlying byte-array used by this <see cref="Buffer"/> instance.
        /// </summary>
        private byte[] _bytes;

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

        public static Buffer OfSize(int size)
        {
            var buffer = Get();
            ArrayPool.Return(buffer._bytes);
            buffer._bytes = ArrayPool.Get(size);
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
            return new Buffer(Packet.BufferSize);
        }

        /// <summary>
        /// Creates a new instance with the specified size.
        /// </summary>
        private Buffer(int size) => _bytes = new byte[size];

        public void Write(byte value)
        {
            EnsureSize(Size + sizeof(byte));
            Write(value, Size);
            Size += sizeof(byte);
        }
        
        public void Write(short value)
        {
            EnsureSize(Size + sizeof(short));
            Write(value, Size);
            Size += sizeof(short);
        }

        public void Write(int value)
        {
            EnsureSize(Size + sizeof(int));
            Write(value, Size);
            Size += sizeof(int);
        }

        public void Write(long value)
        {
            EnsureSize(Size + sizeof(long));
            Write(value, Size);
            Size += sizeof(long);
        }

        public void Write(byte value, int offset)
        {
            _bytes[offset] = value;
        }
        
        public void Write(short value, int offset)
        {
            _bytes[offset + 0] = (byte) (value >> 0);
            _bytes[offset + 1] = (byte) (value >> 8);
        }
        
        public void Write(int value, int offset)
        {
            _bytes[offset + 0] = (byte) (value >> 0);
            _bytes[offset + 1] = (byte) (value >> 8);
            _bytes[offset + 2] = (byte) (value >> 16);
            _bytes[offset + 3] = (byte) (value >> 24);
        }

        public void Write(long value, int offset)
        {
            _bytes[offset + 0] = (byte) (value >> 0);
            _bytes[offset + 1] = (byte) (value >> 8);
            _bytes[offset + 2] = (byte) (value >> 16);
            _bytes[offset + 3] = (byte) (value >> 24);
            _bytes[offset + 4] = (byte) (value >> 32);
            _bytes[offset + 5] = (byte) (value >> 40);
            _bytes[offset + 6] = (byte) (value >> 48);
            _bytes[offset + 7] = (byte) (value >> 56);
        }
        
        public void WriteVarInt(int value)
        {
            var bytesNeeded = VarIntBytesNeeded(value);
            EnsureSize(Size + bytesNeeded);
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
        
        private void EnsureSize(int requiredSize)
        {
            if (Bytes.Length >= requiredSize) return;
            
            var biggerBuffer = ArrayPool.Get(requiredSize);
            Array.Copy(_bytes, biggerBuffer, Size);
            ArrayPool.Return(_bytes);
            _bytes = biggerBuffer;
        }

        public byte ReadByte()
        {
            if (Size - ReadPosition < sizeof(byte))
                throw new InvalidOperationException($"Could not read value of type '{typeof(byte)}' (out-of-bounds bytes).");

            var value = ReadByte(offset: ReadPosition);
            ReadPosition += sizeof(byte);
            return value;
        }
        
        public short ReadShort()
        {
            if (Size - ReadPosition < sizeof(short))
                throw new InvalidOperationException($"Could not read value of type '{typeof(short)}' (out-of-bounds bytes).");
            
            var value = ReadShort(offset: ReadPosition);
            ReadPosition += sizeof(short);
            return value;
        }
        
        public int ReadInt()
        {
            if (Size - ReadPosition < sizeof(int))
                throw new InvalidOperationException($"Could not read value of type '{typeof(int)}' (out-of-bounds bytes).");

            var value = ReadInt(offset: ReadPosition);
            ReadPosition += sizeof(int);
            return value;
        }

        public long ReadLong()
        {
            if (Size - ReadPosition < sizeof(long))
                throw new InvalidOperationException($"Could not read value of type '{typeof(long)}' (out-of-bounds bytes).");
            
            var value = ReadLong(offset: ReadPosition);
            ReadPosition += sizeof(long);
            return value;
        }
        
        public byte ReadByte(int offset)
        {
            return _bytes[offset];
        }

        public short ReadShort(int offset)
        {
            var b0 = (int) _bytes[offset + 0];
            var b1 = (int) _bytes[offset + 1];
            
            return (short) (b0 | b1 << 8);
        }
        
        public int ReadInt(int offset)
        {
            var b0 = (int) _bytes[offset + 0];
            var b1 = (int) _bytes[offset + 1];
            var b2 = (int) _bytes[offset + 2];
            var b3 = (int) _bytes[offset + 3];
            
            return b0 | b1 << 8 | b2 << 16 | b3 << 24;
        }

        public long ReadLong(int offset)
        {
            var b0 = (long) _bytes[offset + 0];
            var b1 = (long) _bytes[offset + 1];
            var b2 = (long) _bytes[offset + 2];
            var b3 = (long) _bytes[offset + 3];
            var b4 = (long) _bytes[offset + 4];
            var b5 = (long) _bytes[offset + 5];
            var b6 = (long) _bytes[offset + 6];
            var b7 = (long) _bytes[offset + 7];
            
            return b0 | b1 << 8 | b2 << 16 | b3 << 24 | b4 << 32 | b5 << 40 | b6 << 48 | b7 << 56;
        }

        public int ReadVarInt(out int bytesRead)
        {
            var varInt = ReadVarInt(ReadPosition, out bytesRead);
            ReadPosition += bytesRead;
            return varInt;
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

                if (_bytes.Length > Packet.BufferSize)
                {
                    ArrayPool.Return(_bytes);
                    _bytes = ArrayPool.Get(Packet.BufferSize);
                }

                BufferPool.Enqueue(this);
                _isInPool = true;
                return true;
            }
        }
    }
}
