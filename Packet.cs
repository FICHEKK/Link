using System;
using System.Collections.Generic;

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
        /// Direct reference to the underlying buffer (defensive copy will <b>not</b> be made).
        /// </summary>
        public byte[] Buffer { get; set; }

        /// <summary>
        /// Returns <see cref="PacketWriter"/> that can write data to this packet.
        /// </summary>
        public PacketWriter Writer { get; }

        /// <summary>
        /// Returns <see cref="PacketReader"/> that can read data from this packet.
        /// </summary>
        public PacketReader Reader { get; }

        /// <summary>
        /// <c>true</c> if reference to this packet currently exists in the pool, <c>false</c> otherwise.
        /// </summary>
        private bool IsInPool { get; set; }

        /// <summary>
        /// Returns a packet with defined ID and delivery method.
        /// </summary>
        /// <param name="id">Indicates packet type to allow the receiver to successfully decode its contents.</param>
        /// <param name="delivery">Defines the way this packet should be delivered to the remote destination.</param>
        public static Packet Get(ushort id, Delivery delivery = Delivery.Unreliable)
        {
            var packet = Get(HeaderType.Data);
            packet.Writer.Write((byte) delivery);
            packet.Writer.Write(id);
            packet.Reader.Position = 2;
            return packet;
        }

        internal static Packet Get(HeaderType headerType)
        {
            var packet = Get();
            packet.Writer.Write((byte) headerType);
            packet.Reader.Position = 1;
            return packet;
        }

        internal static Packet Copy(Packet packet)
        {
            var packetCopy = Get();
            Array.Copy(packet.Buffer, packetCopy.Buffer, length: packet.Writer.Position);

            packetCopy.Writer.Position = packet.Writer.Position;
            packetCopy.Reader.Position = packet.Reader.Position;
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
                    packet.Writer.Position = 0;
                    packet.Reader.Position = 0;
                    packet.IsInPool = false;
                    return packet;
                }
            }

            TotalAllocationCount++;
            return new Packet(MaxSize);
        }

        private Packet(int size)
        {
            Buffer = new byte[size];
            Writer = new PacketWriter(this);
            Reader = new PacketReader(this);
        }

        /// <summary>
        /// Returns this packet to the pool unless it is already in the pool
        /// or its size exceeds <see cref="MaxSizeInPool"/> bytes.
        /// </summary>
        public void Return()
        {
            lock (PacketPool)
            {
                if (IsInPool)
                {
                    Log.Error("Attempt was made to return a packet that is already in pool.");
                    return;
                }

                if (Buffer.Length > MaxSizeInPool)
                {
                    Log.Info($"Big packet ({Buffer.Length} bytes) was not returned to the pool to preserve memory.");
                    return;
                }

                PacketPool.Enqueue(this);
                IsInPool = true;
            }
        }
    }
}
