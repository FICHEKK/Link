using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Core;
using UnityEngine;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a single message of arbitrary data that can be sent over the network.
    /// </summary>
    public abstract partial class Packet
    {
        private const int DefaultBufferSize = 32;
        private const int BufferExpansionFactor = 2;
        private static readonly Encoding Encoding = Encoding.UTF8;

        private static readonly Dictionary<Type, short> PacketTypeToPacketHash = new Dictionary<Type, short>();
        private static readonly Dictionary<short, Func<Packet>> PacketHashToPacketFactory = new Dictionary<short, Func<Packet>>();

        static Packet()
        {
            var packetHashToPacketType = new Dictionary<short, Type>();

            foreach (var packetType in typeof(Packet).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(Packet)) && !type.IsAbstract))
            {
                var packetHash = (short) packetType.FullName.GetStableHashCode();

                if (packetHashToPacketType.TryGetValue(packetHash, out var usedByPacketType))
                {
                    Debug.LogError($"Packet hashing collision: '{packetType.FullName}' and '{usedByPacketType.FullName}'. Please rename one of the classes.");
                    continue;
                }

                packetHashToPacketType.Add(packetHash, packetType);
                PacketTypeToPacketHash.Add(packetType, packetHash);
                PacketHashToPacketFactory.Add(packetHash, () => (Packet) Activator.CreateInstance(packetType));
            }
        }

        private byte[] _buffer = new byte[DefaultBufferSize];
        private int _readPosition;
        private int _writePosition;

        /// <summary>
        /// Returns the number of bytes of data that this packet is currently holding.
        /// </summary>
        public int Size => _writePosition;

        /// <summary>
        /// Returns the appropriate packet instance based on the provided byte array.
        /// </summary>
        public static Packet CreateFrom(byte[] bytes)
        {
            var packetHash = BitConverter.ToInt16(bytes, startIndex: 0);

            if (!PacketHashToPacketFactory.TryGetValue(packetHash, out var packetFactory))
            {
                Debug.LogError($"Could not create packet with hash {packetHash}.");
                return null;
            }

            var packet = packetFactory();
            packet._buffer = bytes;
            packet._writePosition = bytes.Length; // Skip the bytes of the given array as to not overwrite it.
            packet._readPosition = sizeof(short); // Skip the first 2 bytes as those contain packet hash.
            packet.ReadPayload();
            return packet;
        }

        /// <summary>
        /// Sends the packet at its current state to the provided end point.
        /// Only part of the buffer that has been written to will be sent.
        /// This method should be called after the packet has been filled with data.
        /// </summary>
        public void Send(Socket socket, EndPoint receiverEndPoint)
        {
            Write(PacketTypeToPacketHash[GetType()]);
            WritePayload();
            socket.BeginSendTo(_buffer, _writePosition, receiverEndPoint);
        }

        protected abstract void WritePayload();
        protected abstract void ReadPayload();
    }
}
