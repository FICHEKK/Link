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
        private const int DefaultMaxSize = EthernetMtu - MaxIpHeaderSize - UdpHeaderSize;

        /// <summary>
        /// Minimum safe UDP payload size that will not cause fragmentation.
        /// </summary>
        private const int MinSize = 576 - MaxIpHeaderSize - UdpHeaderSize;

        /// <summary>
        /// Maximum number of data bytes that can be transferred in a single Ethernet frame.
        /// </summary>
        private const int EthernetMtu = 1500;

        /// <summary>
        /// Internet Protocol (IP) maximum header size, in bytes.
        /// </summary>
        private const int MaxIpHeaderSize = 60;

        /// <summary>
        /// User Datagram Protocol (UDP) header size, in bytes.
        /// </summary>
        private const int UdpHeaderSize = 8;

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
        public int Size => _writePosition;

        /// <summary>
        /// If packet is read-only, further write operations are not allowed.
        /// </summary>
        public bool IsReadOnly => _isReadOnly;

        /// <summary>
        /// Direct reference to the underlying buffer (defensive copy will <b>not</b> be made).
        /// </summary>
        internal byte[] Buffer
        {
            get => _isInPool ? throw new InvalidOperationException("Cannot get buffer of a packet that is in pool.") : _buffer;
            private set => _buffer = _isInPool ? throw new InvalidOperationException("Cannot set buffer of a packet that is in pool.") : value;
        }

        private byte[] _buffer;
        private int _writePosition;
        private bool _isReadOnly;
        private bool _isInPool;

        /// <summary>
        /// Returns a packet with defined ID and delivery method.
        /// </summary>
        /// <param name="packetId">Defines packet type which allows receiver to decode its contents.</param>
        /// <param name="delivery">Defines the way this packet should be delivered to the remote destination.</param>
        public static Packet Get(ushort packetId, Delivery delivery) => Get(packetId, (byte) delivery);

        /// <summary>
        /// Returns a packet with defined ID and channel ID.
        /// </summary>
        /// <param name="packetId">Defines packet type which allows receiver to decode its contents.</param>
        /// <param name="channelId">Defines on which channel this packet should be sent.</param>
        public static Packet Get(ushort packetId, byte channelId)
        {
            var packet = Get(HeaderType.Data);
            packet.Write(channelId);
            packet.Write(packetId);
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

            copy._writePosition = packet._writePosition;
            return copy;
        }

        internal static Packet From(byte[] buffer, int size)
        {
            var packet = Get();
            Array.Copy(buffer, packet._buffer, size);

            packet._writePosition = size;
            return packet;
        }

        internal static Packet With(byte[] buffer)
        {
            var packet = Get();
            packet.Buffer = buffer;
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
                    packet._writePosition = 0;
                    packet._isReadOnly = false;
                    packet._isInPool = false;
                    return packet;
                }
            }

            TotalAllocationCount++;
            return new Packet(MaxSize);
        }

        private Packet(int size) =>
            _buffer = new byte[size];

        public void Write(string value)
        {
            if (value is null) throw new InvalidOperationException("Cannot write null string to a packet.");
            WriteArray(Encoding.GetBytes(value));
        }

        public unsafe void Write<T>(T value) where T : unmanaged
        {
            var bytesToWrite = sizeof(T);
            EnsureBufferSize(_writePosition + bytesToWrite);
            Buffer.Write(value, _writePosition);
            _writePosition += bytesToWrite;
        }

        public unsafe void WriteArray<T>(T[] array) where T : unmanaged
        {
            if (array is null) throw new InvalidOperationException("Cannot write null array to a packet.");
            
            var bytesToWrite = sizeof(int) + array.Length * sizeof(T);
            EnsureBufferSize(_writePosition + bytesToWrite);
            Buffer.WriteArray(array, _writePosition);
            _writePosition += bytesToWrite;
        }

        public unsafe void WriteSlice<T>(T[] array, int start, int length) where T : unmanaged
        {
            if (array is null) throw new InvalidOperationException("Cannot write slice of null array to a packet.");
            
            var bytesToWrite = length * sizeof(T);
            EnsureBufferSize(_writePosition + bytesToWrite);
            Buffer.WriteSlice(array, start, length, _writePosition);
            _writePosition += bytesToWrite;
        }

        private void EnsureBufferSize(int requiredBufferSize)
        {
            if (_isReadOnly) throw new InvalidOperationException("Cannot write to a read-only packet.");

            var currentBuffer = Buffer;
            if (currentBuffer.Length >= requiredBufferSize) return;

            var expandedBuffer = new byte[Math.Max(currentBuffer.Length * 2, requiredBufferSize)];
            Array.Copy(currentBuffer, expandedBuffer, _writePosition);
            Buffer = expandedBuffer;
        }

        /// <summary>
        /// Makes this packet read-only, which prevents further write operations.
        /// </summary>
        public Packet AsReadOnly()
        {
            if (_isReadOnly) throw new InvalidOperationException("Packet is already read-only.");
            
            _isReadOnly = true;
            return this;
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
