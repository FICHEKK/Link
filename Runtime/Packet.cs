using System;
using System.Collections.Generic;
using System.Text;

namespace Link
{
    /// <summary>
    /// Represents a single message of arbitrary data that can be sent over the network.
    /// </summary>
    public sealed class Packet
    {
        /// <summary>
        /// Maximum allowed packet size, in bytes. This value must be chosen carefully to avoid
        /// fragmentation on the network layer. Any packets that require bigger size must use a
        /// fragmented channel which will perform fragmentation and reassembly on the application
        /// layer. Defaults to <see cref="DefaultMaxSize"/> bytes.
        /// </summary>
        /// <remarks>
        /// This value cannot be set to a value that is lower than <see cref="MinSize"/> bytes.
        /// </remarks>
        public static int MaxSize
        {
            get => _maxSize;
            set => _maxSize = value >= MinSize ? value : throw new ArgumentOutOfRangeException(nameof(MaxSize));
        }

        /// <summary>
        /// Backing field of <see cref="MaxSize"/> property.
        /// </summary>
        private static int _maxSize = DefaultMaxSize;

        /// <summary>
        /// Default maximum packet size. If network layer fragmentation occurs when using
        /// this buffer size, consider lowering the value stored in <see cref="MaxSize"/>.
        /// </summary>
        private const int DefaultMaxSize = EthernetMtu - IpHeaderSize - UdpHeaderSize;

        /// <summary>
        /// Maximum number of data bytes that can be transferred in a single Ethernet frame.
        /// </summary>
        private const int EthernetMtu = 1500;

        /// <summary>
        /// Internet Protocol (IP) standard header size, in bytes.
        /// </summary>
        private const int IpHeaderSize = 20;

        /// <summary>
        /// User Datagram Protocol (UDP) header size, in bytes.
        /// </summary>
        private const int UdpHeaderSize = 8;
        
        /// <summary>
        /// Minimum safe UDP payload size (in bytes) that will not cause fragmentation.
        /// </summary>
        public const int MinSize = 508;

        /// <summary>
        /// Maximum allowed packet size of a pooled packet. Trying to return a packet with bigger
        /// buffer to the pool is going to result in packet being rejected by the pool. This is needed
        /// as a measure to prevent allocating too much memory, which would happen if there we too many
        /// big packets stored in the pool.
        /// </summary>
        private const int MaxSizeInPool = ushort.MaxValue;

        /// <summary>
        /// Collection of reusable packet instances used to avoid frequent memory allocations.
        /// </summary>
        private static readonly Queue<Packet> PacketPool = new();

        /// <summary>
        /// Represents total number of new packet allocations. This value should eventually stagnate if
        /// packets are properly returned (unless big packets are created frequently, which will not be
        /// returned to preserve memory). If this value keeps on increasing, that is a clear sign that
        /// there is a packet leak - somewhere a packet is taken but not returned to the pool.
        /// </summary>
        public static int TotalAllocationCount { get; private set; }

        /// <summary>
        /// Encoding used for reading and writing <see cref="string"/> values.
        /// </summary>
        public static Encoding Encoding { get; set; } = Encoding.UTF8;

        /// <summary>
        /// Returns the number of bytes currently contained in this packet.
        /// </summary>
        public int Size { get; private set; }

        /// <summary>
        /// Direct reference to the underlying buffer (defensive copy will <b>not</b> be made).
        /// </summary>
        internal byte[] Buffer
        {
            get => _isInPool ? throw new InvalidOperationException("Cannot get buffer of a packet that is in pool.") : _buffer;
            private set => _buffer = _isInPool ? throw new InvalidOperationException("Cannot set buffer of a packet that is in pool.") : value;
        }

        private byte[] _buffer;
        private bool _isInPool;

        /// <summary>
        /// Returns a packet that will be sent using specified delivery method.
        /// </summary>
        public static Packet Get(Delivery delivery) => Get((byte) delivery);

        /// <summary>
        /// Returns a packet that will be sent on the specified channel.
        /// </summary>
        public static Packet Get(byte channelId)
        {
            var packet = Get(HeaderType.Data);
            packet.Write(channelId);
            return packet;
        }

        internal static Packet Get(HeaderType headerType)
        {
            var packet = Get();
            packet.Write((byte) headerType);
            return packet;
        }

        internal static Packet Copy(Packet packet)
        {
            // Since packet is provided from the outside source, property
            // getters need to be used to ensure packet is not in the pool.
            var copy = Get();
            Array.Copy(packet.Buffer, copy._buffer, packet.Size);
            copy.Size = packet.Size;
            return copy;
        }

        internal static Packet From(byte[] buffer, int size)
        {
            var packet = Get();
            Array.Copy(buffer, packet._buffer, size);
            packet.Size = size;
            return packet;
        }

        internal static Packet With(byte[] buffer)
        {
            var packet = Get();
            packet.Buffer = buffer;
            packet.Size = buffer.Length;
            return packet;
        }

        /// <summary>
        /// Returns an empty packet.
        /// </summary>
        public static Packet Get()
        {
            lock (PacketPool)
            {
                if (PacketPool.Count > 0)
                {
                    var packet = PacketPool.Dequeue();
                    packet.Size = 0;
                    packet._isInPool = false;
                    return packet;
                }
            }

            TotalAllocationCount++;
            return new Packet(MaxSize);
        }

        private Packet(int size) =>
            _buffer = new byte[size];

        public Packet Write(string value)
        {
            if (value is null) throw new InvalidOperationException("Cannot write null string to a packet.");
            
            WriteArray(Encoding.GetBytes(value));
            return this;
        }

        public unsafe Packet Write<T>(T value) where T : unmanaged
        {
            var bytesToWrite = sizeof(T);
            EnsureBufferSize(Size + bytesToWrite);
            Buffer.Write(value, Size);
            Size += bytesToWrite;
            return this;
        }

        public unsafe Packet WriteArray<T>(T[] array) where T : unmanaged
        {
            if (array is null) throw new InvalidOperationException("Cannot write null array to a packet.");
            
            var bytesToWrite = sizeof(int) + array.Length * sizeof(T);
            EnsureBufferSize(Size + bytesToWrite);
            Buffer.WriteArray(array, Size);
            Size += bytesToWrite;
            return this;
        }

        public unsafe Packet WriteSlice<T>(T[] array, int start, int length) where T : unmanaged
        {
            if (array is null) throw new InvalidOperationException("Cannot write slice of null array to a packet.");
            
            var bytesToWrite = length * sizeof(T);
            EnsureBufferSize(Size + bytesToWrite);
            Buffer.WriteSlice(array, start, length, Size);
            Size += bytesToWrite;
            return this;
        }

        private void EnsureBufferSize(int requiredBufferSize)
        {
            var currentBuffer = Buffer;
            if (currentBuffer.Length >= requiredBufferSize) return;

            var expandedBuffer = new byte[Math.Max(currentBuffer.Length * 2, requiredBufferSize)];
            Array.Copy(currentBuffer, expandedBuffer, Size);
            Buffer = expandedBuffer;
        }

        /// <summary>
        /// Returns this packet to the pool unless it is already in the pool
        /// or its size exceeds <see cref="MaxSizeInPool"/> bytes.
        /// </summary>
        /// <returns><c>true</c> if packet was successfully returned to the pool, <c>false</c> otherwise.</returns>
        public bool Return()
        {
            lock (PacketPool)
            {
                if (_isInPool)
                {
                    Log.Error("Attempt was made to return a packet that is already in pool.");
                    return false;
                }

                if (_buffer.Length > MaxSizeInPool)
                {
                    Log.Info($"Big packet ({_buffer.Length} bytes) was not returned to the pool to preserve memory.");
                    return false;
                }

                PacketPool.Enqueue(this);
                _isInPool = true;
                return true;
            }
        }
    }
}
