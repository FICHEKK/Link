using System.Collections.Generic;
using System.Text;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a single message of arbitrary data that can be sent over the network.
    /// </summary>
    public sealed class Packet
    {
        // TODO - Find out what this value should actually be.
        private const int MaxBufferSize = 1500;

        public static readonly Encoding Encoding = Encoding.UTF8;
        private static readonly Queue<Packet> PacketPool = new Queue<Packet>();

        /// <summary>
        /// Represents total number of new packet allocations. This value should eventually stagnate
        /// if packets are properly returned. If this value keeps on increasing, that is a clear sign
        /// that there is a packet leak - somewhere a packet is taken but not returned to the pool.
        /// </summary>
        public static int TotalAllocationCount { get; private set; }

        internal HeaderType HeaderType => (HeaderType) Buffer[0];

        public byte[] Buffer { get; set; }
        public PacketWriter Writer { get; }
        public PacketReader Reader { get; }

        public static Packet Get(ushort id) =>
            Get(id, DeliveryMethod.Unreliable);

        public static Packet Get(ushort id, DeliveryMethod deliveryMethod)
        {
            var packet = Get((HeaderType) deliveryMethod.Id);
            packet.Reader.ReadPosition = deliveryMethod.HeaderSizeInBytes;
            packet.Writer.WritePosition = deliveryMethod.HeaderSizeInBytes;
            packet.Writer.Write(id);
            return packet;
        }

        internal static Packet Get(HeaderType headerType)
        {
            var packet = Get();
            packet.Writer.Write((byte) headerType);
            packet.Reader.ReadPosition = packet.Writer.WritePosition;
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
