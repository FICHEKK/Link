using System;
using System.Text;

namespace Link
{
    /// <summary>
    /// Represents a single message of arbitrary data that can be sent over the network.
    /// </summary>
    public readonly ref struct Packet
    {
        /// <summary>
        /// Sets the default <see cref="Link.Buffer"/> size to <see cref="DefaultMaxSize"/> bytes.
        /// </summary>
        static Packet() => Buffer.DefaultSize = DefaultMaxSize;

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
            get => Buffer.DefaultSize;
            set => Buffer.DefaultSize = value >= MinSize ? value : throw new ArgumentOutOfRangeException(nameof(MaxSize));
        }

        /// <summary>
        /// Default maximum packet size. If network layer fragmentation occurs when using
        /// this buffer size, consider lowering the value stored in <see cref="MaxSize"/>.
        /// </summary>
        private const int DefaultMaxSize = EthernetMtu - IpHeaderSize - UdpHeaderSize;

        /// <summary>
        /// Maximum number of data bytes that can be transferred in a single Ethernet frame.
        /// </summary>
        private const int EthernetMtu = 1500;

        /// <summary>
        /// Internet Protocol (IP) standard header size, in bytes.
        /// </summary>
        private const int IpHeaderSize = 20;

        /// <summary>
        /// User Datagram Protocol (UDP) header size, in bytes.
        /// </summary>
        private const int UdpHeaderSize = 8;
        
        /// <summary>
        /// Minimum safe UDP payload size (in bytes) that will not cause fragmentation.
        /// </summary>
        public const int MinSize = 508;

        /// <summary>
        /// Encoding used for converting <see cref="string"/> to byte-array and vice-versa.
        /// </summary>
        public static Encoding Encoding { get; set; } = Encoding.UTF8;

        /// <summary>
        /// Returns the number of bytes currently contained in this packet.
        /// </summary>
        public int Size => Buffer.Size;

        /// <summary>
        /// Direct reference to the underlying buffer (defensive copy will <b>not</b> be made).
        /// </summary>
        internal Buffer Buffer { get; }

        /// <summary>
        /// Returns a packet that will be sent using specified delivery method.
        /// </summary>
        public static Packet Get(Delivery delivery) => Get((byte) delivery);

        /// <summary>
        /// Returns a packet that will be sent on the specified channel.
        /// </summary>
        public static Packet Get(byte channelId) => Get(HeaderType.Data).Write(channelId);

        /// <summary>
        /// Returns a packet with specific <see cref="HeaderType"/>.
        /// </summary>
        internal static Packet Get(HeaderType headerType) => Get().Write(headerType);

        /// <summary>
        /// Returns an empty packet.
        /// </summary>
        internal static Packet Get() => new(Buffer.Get());

        /// <summary>
        /// Creates a new <see cref="Packet"/> that wraps around specified <see cref="Link.Buffer"/>.
        /// </summary>
        internal Packet(Buffer buffer) => Buffer = buffer;

        /// <summary>
        /// Writes a <see cref="string"/> to this packet (using encoding defined by <see cref="Encoding"/>).
        /// </summary>
        public Packet Write(string value)
        {
            if (value is null) throw new InvalidOperationException("Cannot write null string to a packet.");
            
            WriteArray(Encoding.GetBytes(value));
            return this;
        }

        /// <summary>
        /// Writes a value of specified type to this packet.
        /// </summary>
        public Packet Write<T>(T value) where T : unmanaged
        {
            Buffer.Write(value);
            return this;
        }

        /// <summary>
        /// Writes an entire array to this packet.
        /// </summary>
        /// <param name="array">Array to write.</param>
        /// <param name="writeLength">
        /// If <c>true</c>, length of the given array will be written before writing
        /// array elements, otherwise only array elements will be written.
        /// </param>
        public Packet WriteArray<T>(T[] array, bool writeLength = true) where T : unmanaged =>
            WriteArray(array, start: 0, length: array.Length, writeLength);

        /// <summary>
        /// Writes the portion of an array to this packet. 
        /// </summary>
        /// <param name="array">Array to write.</param>
        /// <param name="start">Index in the array from which to start writing elements.</param>
        /// <param name="length">Number of elements to write.</param>
        /// <param name="writeLength">If <c>true</c>, length will be written before elements, otherwise only elements will be written.</param>
        public Packet WriteArray<T>(T[] array, int start, int length, bool writeLength = true) where T : unmanaged
        {
            if (array is null) throw new InvalidOperationException("Cannot write null array to a packet.");
            
            if (writeLength) Buffer.Write(array.Length);
            Buffer.WriteArray(array, start, length);
            return this;
        }

        /// <summary>
        /// Disposes of this packet by returning previously borrowed <see cref="Link.Buffer"/> to the pool.
        /// </summary>
        public void Return() => Buffer.Return();
    }
}
