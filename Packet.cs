using System;
using System.Collections.Generic;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a single message of arbitrary data that can be sent over the network.
    /// </summary>
    public sealed class Packet
    {
        /// <summary>
        /// Maximum allowed packet size, in bytes. This value is chosen in order to avoid fragmentation
        /// on the network layer. Any packets that require bigger size must use a fragmented channel
        /// which will perform fragmentation and reassembly on the application layer.
        /// </summary>
        public const int MaxSize = EthernetMtu - MaxIpHeaderSize - UdpHeaderSize;

        /// <summary>
        /// Maximum allowed packet size of a pooled packet. Trying to return a packet with bigger
        /// buffer to the pool is going to result in packet being rejected by the pool. This is needed
        /// as a measure to prevent allocating too much memory, which would happen if there we too many
        /// big packets stored in the pool.
        /// </summary>
        private const int MaxSizeInPool = ushort.MaxValue - UdpHeaderSize;

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
        /// Defines how many bytes are needed to store header information for each delivery type.
        /// </summary>
        private static readonly int[] DeliveryHeaderSizes = new int[Enum.GetValues(typeof(Delivery)).Length];

        static Packet()
        {
            DeliveryHeaderSizes[(int) Delivery.Unreliable] = 1;
            DeliveryHeaderSizes[(int) Delivery.Sequenced] = 3;
            DeliveryHeaderSizes[(int) Delivery.ReliableUnordered] = 3;
            DeliveryHeaderSizes[(int) Delivery.Reliable] = 3;
            DeliveryHeaderSizes[(int) Delivery.FragmentedUnordered] = 5;
            DeliveryHeaderSizes[(int) Delivery.Fragmented] = 5;
        }

        public byte[] Buffer { get; set; }
        public PacketWriter Writer { get; }
        public PacketReader Reader { get; }
        private bool IsInPool { get; set; }

        public static Packet Get(ushort id, Delivery delivery = Delivery.Unreliable)
        {
            var packet = Get(HeaderType.Data, delivery);
            var headerSize = DeliveryHeaderSizes[(int) delivery];
            packet.Reader.Position = headerSize;
            packet.Writer.Position = headerSize;
            packet.Writer.Write(id);
            return packet;
        }

        internal static Packet Get(HeaderType headerType, Delivery delivery = Delivery.Unreliable)
        {
            var packet = Get();
            var header = (int) headerType | (int) delivery << 4;
            packet.Writer.Write((byte) header);
            packet.Reader.Position = 1;
            return packet;
        }

        internal static Packet From(byte[] datagram, int bytesReceived)
        {
            var packet = Get();
            Array.Copy(datagram, packet.Buffer, bytesReceived);

            var headerSize = DeliveryHeaderSizes[datagram[0] >> 4];
            packet.Reader.Position = headerSize;
            packet.Writer.Position = bytesReceived;
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
