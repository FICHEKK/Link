using System.Collections.Generic;
using System.Text;
using Networking.Transport.Channels;

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

        public static readonly Encoding Encoding = Encoding.UTF8;
        private static readonly Queue<Packet> PacketPool = new();

        /// <summary>
        /// Represents total number of new packet allocations. This value should eventually stagnate
        /// if packets are properly returned. If this value keeps on increasing, that is a clear sign
        /// that there is a packet leak - somewhere a packet is taken but not returned to the pool.
        /// </summary>
        public static int TotalAllocationCount { get; private set; }

        public byte[] Buffer { get; set; }
        public PacketWriter Writer { get; }
        public PacketReader Reader { get; }

        // TODO - Remove this overload and require developer to specify channel.
        public static Packet Get(ushort id) => Get(id, Channel.Unreliable);

        public static Packet Get(ushort id, Channel channel)
        {
            var packet = Get(HeaderType.Data, channel);
            packet.Reader.ReadPosition = channel.HeaderSizeInBytes;
            packet.Writer.WritePosition = channel.HeaderSizeInBytes;
            packet.Writer.Write(id);
            return packet;
        }

        internal static Packet Get(HeaderType headerType) => Get(headerType, Channel.Unreliable);

        internal static Packet Get(HeaderType headerType, Channel channel)
        {
            var packet = Get();
            var header = (int) headerType | channel.Id << 4;
            packet.Writer.Write((byte) header);
            packet.Reader.ReadPosition = 1;
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
