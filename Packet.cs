using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a single message of arbitrary data that can be sent over the network.
    /// </summary>
    public partial class Packet : IDisposable
    {
        private static readonly Encoding Encoding = Encoding.UTF8;

        private readonly byte[] _buffer;
        private int _readPosition;
        private int _writePosition;

        /// <summary>
        /// Uniquely identifies packet contents. In order for the packet receiver to be able to
        /// interpret packet contents, each packet type (not each packet instance) must
        /// have a unique identifier.
        /// </summary>
        public short Type { get; private set; }

        /// <summary>
        /// Returns the number of bytes of data that this packet is currently holding.
        /// </summary>
        public int Size => _writePosition;

        /// <summary>
        /// In order to minimize allocations, all of the packets should be "created"
        /// using the static factory method which enforces object pooling to reuse packets.
        /// That is why there is no public constructors available.
        /// </summary>
        private Packet(byte[] buffer) => _buffer = buffer;

        /// <summary>
        /// Returns a packet with provided type identifier written at the start of the packet.
        /// This method is called when sending the packet.
        /// </summary>
        public static Packet OfType(short type)
        {
            //var packet = PacketPool.Borrow();
            var packet = new Packet(new byte[1024]);
            packet.Write(type);
            packet.Type = packet.ReadShort();
            return packet;
        }

        /// <summary>
        /// Returns a packet constructed from the provided byte buffer.
        /// This method is called when receiving the packet.
        /// </summary>
        public static Packet OfBytes(byte[] bytes, int length)
        {
            //var packet = PacketPool.Borrow();
            var packet = new Packet(new byte[1024]);
            packet.Write(bytes, length);
            packet.Type = packet.ReadShort();
            return packet;
        }

        /// <summary>
        /// Sends the packet at its current state to the provided end point.
        /// Only part of the buffer that has been written to will be sent.
        /// This method should be called after the packet has been filled with data.
        /// </summary>
        public void Send(Socket socket, EndPoint receiverEndPoint) => socket.BeginSendTo(
            buffer: _buffer,
            offset: 0,
            size: _writePosition,
            socketFlags: SocketFlags.None,
            remoteEP: receiverEndPoint,
            callback: null,
            state: null
        );

        public void Dispose()
        {
            _readPosition = 0;
            _writePosition = 0;

            //PacketPool.Return(this);
        }
    }
}
