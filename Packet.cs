using System.Collections.Generic;
using System.Text;
using Networking.Core;
using UnityEngine;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a single message of arbitrary data that can be sent over the network.
    /// </summary>
    public sealed class Packet
    {
        // TODO - Find out what this value should actually be.
        private const int MaxBufferSize = 1500;
        private const int DefaultPacketPoolSize = 15;

        public static readonly Encoding Encoding = Encoding.UTF8;
        private static readonly Queue<Packet> PacketPool = new Queue<Packet>(DefaultPacketPoolSize);

        static Packet()
        {
            for (var i = 0; i < DefaultPacketPoolSize; i++)
            {
                PacketPool.Enqueue(new Packet(MaxBufferSize));
            }
        }

        public ushort Id { get; private set; }
        public byte[] Buffer { get; set; }
        public PacketWriter Writer { get; }
        public PacketReader Reader { get; }

        public static Packet Get(ushort id)
        {
            lock (PacketPool)
            {
                if (PacketPool.Count == 0)
                {
                    const string line1 = "Packet pool has been emptied, which means that:";
                    const string line2 = "A) Packet pool initial size is simply too small.";
                    const string line3 = "B) There is a memory leak and packet is not getting returned.";

                    // TODO - Refactor into "PacketPoolEmptyException" - method "EnsurePoolIsNotEmpty"
                    Debug.LogError(line1.NewLine() + line2.NewLine() + line3.NewLine());
                }

                var packet = PacketPool.Count > 0 ? PacketPool.Dequeue() : new Packet(MaxBufferSize);
                packet.Id = id;
                packet.Writer.Write(id);
                packet.Reader.ReadPosition = sizeof(ushort);
                return packet;
            }
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
                Writer.WritePosition = 0;
                Reader.ReadPosition = 0;
                PacketPool.Enqueue(this);
            }
        }
    }
}
