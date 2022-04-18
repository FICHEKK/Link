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
        /// Represents total number of new packet allocations. This value should eventually stagnate
        /// if packets are properly returned. If this value keeps on increasing, that is a clear sign
        /// that there is a packet leak - somewhere a packet is taken but not returned to the pool.
        /// </summary>
        public static int TotalAllocationCount { get; private set; }

        /// <summary>
        /// Encoding used for reading and writing <see cref="string"/> values.
        /// </summary>
        private static readonly Encoding Encoding = Encoding.UTF8;

        /// <summary>
        /// Gets or sets <see cref="byte"/> at the specified index in this packet.
        /// </summary>
        public byte this[int index]
        {
            get => _buffer[index];
            set => _buffer[index] = _isInPool ? throw new InvalidOperationException("Cannot modify a packet that is in pool.") : value;
        }

        /// <summary>
        /// Direct reference to the underlying buffer (defensive copy will <b>not</b> be made).
        /// </summary>
        internal byte[] Buffer
        {
            get => _isInPool ? throw new InvalidOperationException("Cannot get buffer of a packet that is in pool.") : _buffer;
            set => _buffer = _isInPool ? throw new InvalidOperationException("Cannot set buffer of a packet that is in pool.") : value;
        }

        /// <summary>
        /// Index at which the next write operation will be performed.
        /// </summary>
        public int WritePosition { get; set; }

        /// <summary>
        /// Index at which the next read operation will be performed.
        /// </summary>
        public int ReadPosition { get; set; }

        private byte[] _buffer;
        private bool _isInPool;

        /// <summary>
        /// Returns a packet with defined ID and delivery method.
        /// </summary>
        /// <param name="id">Indicates packet type to allow the receiver to successfully decode its contents.</param>
        /// <param name="delivery">Defines the way this packet should be delivered to the remote destination.</param>
        public static Packet Get(ushort id, Delivery delivery = Delivery.Unreliable)
        {
            var packet = Get(HeaderType.Data);
            packet.Write((byte) delivery);
            packet.Write(id);
            packet.ReadPosition = 2;
            return packet;
        }

        internal static Packet Get(HeaderType headerType)
        {
            var packet = Get();
            packet.Write((byte) headerType);
            packet.ReadPosition = 1;
            return packet;
        }

        internal static Packet Copy(Packet packet)
        {
            // Since packet is provided from the outside source, property
            // getters need to be used to ensure packet is not in the pool.
            var packetCopy = Get();
            Array.Copy(packet.Buffer, packetCopy._buffer, length: packet.WritePosition);

            packetCopy.WritePosition = packet.WritePosition;
            packetCopy.ReadPosition = packet.ReadPosition;
            return packetCopy;
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
                    packet.WritePosition = 0;
                    packet.ReadPosition = 0;
                    packet._isInPool = false;
                    return packet;
                }
            }

            TotalAllocationCount++;
            return new Packet(MaxSize);
        }

        private Packet(int size) =>
            _buffer = new byte[size];

        public void Write(string value) =>
            WriteArray(Encoding.GetBytes(value));

        public unsafe void Write<T>(T value) where T : unmanaged
        {
            var bytesToWrite = sizeof(T);
            EnsureBufferSize(requiredBufferSize: WritePosition + bytesToWrite);
            Buffer.AsSpan().Write(value, WritePosition);
            WritePosition += bytesToWrite;
        }

        public unsafe void WriteArray<T>(T[] array) where T : unmanaged
        {
            var bytesToWrite = sizeof(int) + array.Length * sizeof(T);
            EnsureBufferSize(requiredBufferSize: WritePosition + bytesToWrite);
            Buffer.AsSpan().WriteArray(array, WritePosition);
            WritePosition += bytesToWrite;
        }

        public unsafe void WriteSpan<T>(ReadOnlySpan<T> span) where T : unmanaged
        {
            var bytesToWrite = span.Length * sizeof(T);
            EnsureBufferSize(requiredBufferSize: WritePosition + bytesToWrite);
            Buffer.AsSpan().WriteSpan(span, WritePosition);
            WritePosition += bytesToWrite;
        }

        private void EnsureBufferSize(int requiredBufferSize)
        {
            var currentBuffer = Buffer;
            if (currentBuffer.Length >= requiredBufferSize) return;

            var expandedBuffer = new byte[Math.Max(currentBuffer.Length * 2, requiredBufferSize)];
            Array.Copy(currentBuffer, expandedBuffer, WritePosition);
            Buffer = expandedBuffer;
        }

        public string ReadString()
        {
            var stringByteCount = Read<int>();
            var stringValue = Encoding.GetString(Buffer, ReadPosition, stringByteCount);
            ReadPosition += stringByteCount;
            return stringValue;
        }

        public unsafe T Read<T>() where T : unmanaged
        {
            var value = new ReadOnlySpan<byte>(Buffer).Read<T>(ReadPosition);
            ReadPosition += sizeof(T);
            return value;
        }

        public unsafe T[] ReadArray<T>() where T : unmanaged
        {
            var array = new ReadOnlySpan<byte>(Buffer).ReadArray<T>(ReadPosition);
            ReadPosition += sizeof(int) + array.Length * sizeof(T);
            return array;
        }

        /// <summary>
        /// Returns this packet to the pool unless it is already in the pool
        /// or its size exceeds <see cref="MaxSizeInPool"/> bytes.
        /// </summary>
        public void Return()
        {
            lock (PacketPool)
            {
                if (_isInPool)
                {
                    Log.Error("Attempt was made to return a packet that is already in pool.");
                    return;
                }

                if (_buffer.Length > MaxSizeInPool)
                {
                    Log.Info($"Big packet ({_buffer.Length} bytes) was not returned to the pool to preserve memory.");
                    return;
                }

                PacketPool.Enqueue(this);
                _isInPool = true;
            }
        }
    }
}
