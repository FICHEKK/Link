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
        /// Maximum number of data bytes that can be transferred in a single Ethernet frame.
        /// </summary>
        private const int EthernetMtu = 1500;

        /// <summary>
        /// Internet protocol maximum header size, in bytes.
        /// </summary>
        private const int MaxIPHeaderSize = 60;

        /// <summary>
        /// User Datagram Protocol header size, in bytes.
        /// </summary>
        private const int UdpHeaderSize = 8;

        /// <summary>
        /// Maximum packet size that will not result in packet fragmentation.
        /// </summary>
        private const int MaxBufferSize = EthernetMtu - MaxIPHeaderSize - UdpHeaderSize;

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
            DeliveryHeaderSizes[(int) Delivery.Reliable] = 3;
        }

        public byte[] Buffer { get; set; }
        public PacketWriter Writer { get; }
        public PacketReader Reader { get; }

        public static Packet Get(ushort id, Delivery delivery = Delivery.Unreliable)
        {
            var packet = Get(HeaderType.Data, delivery);
            var headerSize = DeliveryHeaderSizes[(int) delivery];
            packet.Reader.ReadPosition = headerSize;
            packet.Writer.WritePosition = headerSize;
            packet.Writer.Write(id);
            return packet;
        }

        internal static Packet Get(HeaderType headerType, Delivery delivery = Delivery.Unreliable)
        {
            var packet = Get();
            var header = (int) headerType | (int) delivery << 4;
            packet.Writer.Write((byte) header);
            packet.Reader.ReadPosition = 1;
            return packet;
        }

        internal static Packet From(byte[] datagram, int bytesReceived)
        {
            var packet = Get();
            Array.Copy(datagram, packet.Buffer, bytesReceived);

            var headerSize = DeliveryHeaderSizes[datagram[0] >> 4];
            packet.Reader.ReadPosition = headerSize;
            packet.Writer.WritePosition = headerSize;
            return packet;
        }

        public static Packet Get()
        {
            lock (PacketPool)
                if (PacketPool.Count > 0)
                    return PacketPool.Dequeue();

            TotalAllocationCount++;
            return new Packet(MaxBufferSize);
        }

        private Packet(int size)
        {
            Buffer = new byte[size];
            Writer = new PacketWriter(this);
            Reader = new PacketReader(this);
        }

        public void Return()
        {
            Writer.WritePosition = 0;
            Reader.ReadPosition = 0;

            lock (PacketPool) PacketPool.Enqueue(this);
        }
    }
}
