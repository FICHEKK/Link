using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a single message of arbitrary data that can be sent over the network.
    /// </summary>
    public partial class Packet
    {
        private const int DefaultBufferSize = 32;
        private const int BufferExpansionFactor = 2;
        private static readonly Encoding Encoding = Encoding.UTF8;

        private byte[] _buffer;
        private int _readPosition;
        private int _writePosition;

        /// <summary>
        /// Uniquely identifies packet contents. In order for the packet receiver to be able to
        /// interpret packet contents, each packet type (not each packet instance) must
        /// have a unique identifier.
        /// </summary>
        public PacketType Type { get; private set; }

        /// <summary>
        /// Returns the number of bytes of data that this packet is currently holding.
        /// </summary>
        public int Size => _writePosition;

        /// <summary>
        /// Use static methods for creating packets.
        /// </summary>
        private Packet(byte[] buffer) => _buffer = buffer;

        /// <summary>
        /// Returns a packet with provided type identifier written at the start of the packet.
        /// This method is called when sending the packet.
        /// </summary>
        public static Packet OfType(PacketType type)
        {
            var packet = new Packet(new byte[DefaultBufferSize]);
            packet.Write((short) type);
            packet.Type = (PacketType) packet.ReadShort();
            return packet;
        }

        /// <summary>
        /// Returns a packet constructed from the provided byte buffer.
        /// This method is called when receiving the packet.
        /// </summary>
        public static Packet OfBytes(byte[] bytes, int length)
        {
            var packet = new Packet(new byte[length]);
            packet.Write(bytes, length);
            packet.Type = (PacketType) packet.ReadShort();
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
    }
}
