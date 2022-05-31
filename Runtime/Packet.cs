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
        /// Maximum number of data-bytes that can be written to a packet. Any packets that
        /// require bigger size must use a channel that supports fragmentation and reassembly.
        /// <br/><br/>
        /// This value was chosen carefully in order to avoid fragmentation on the network
        /// layer. It is equal to: <see cref="BufferSize"/> - <see cref="HeaderSize"/>.
        /// </summary>
        public const int MaxSize = BufferSize - HeaderSize;
        
        /// <summary>
        /// Packet buffer size, in bytes. This value represents maximum number of bytes that
        /// can be written to a single packet.
        /// <br/><br/>
        /// It includes both header and data bytes and is equal to: Ethernet MTU (1500 bytes)
        /// - IP header size (20 bytes) - UDP header size (8 bytes) = 1472 bytes.
        /// </summary>
        internal const int BufferSize = 1472;
        
        /// <summary>
        /// Consists of header type (1 byte), channel ID (1 byte) and channel header (4 bytes).
        /// </summary>
        internal const int HeaderSize = DataHeaderSize + 4;
        
        /// <summary>
        /// Consists of header type (1 byte) and channel ID (1 byte).
        /// </summary>
        internal const int DataHeaderSize = 2;
        
        /// <summary>
        /// Dummy value that is written to data-packet in order to fill channel header.
        /// </summary>
        private const int ChannelHeaderFill = 0;

        /// <summary>
        /// Encoding used for converting <see cref="string"/> to byte-array and vice-versa.
        /// </summary>
        public static Encoding Encoding
        {
            get => _encoding;
            set => _encoding = value ?? throw new InvalidOperationException("Encoding cannot be set to null.");
        }

        /// <summary>
        /// Backing field of <see cref="Encoding"/> property.
        /// </summary>
        private static Encoding _encoding = Encoding.UTF8;

        /// <summary>
        /// Represents total number of internal buffer allocations made. For each packet made,
        /// there needs to be a buffer that the packet can be write to. These buffers are made
        /// to be reusable in order to avoid triggering garbage collection.
        /// <br/><br/>
        /// However, if a packet is created and not sent, buffer will not get returned to the
        /// internal buffer pool. In that case, <see cref="Return"/> method should be called
        /// to manually return used buffer to the pool.
        /// <br/><br/>
        /// If buffers are properly returned, this value should eventually stagnate, as all of
        /// the allocated buffers will get reused and there will be no need for creating new
        /// ones.
        /// </summary>
        public static int AllocationCount => Buffer.AllocationCount;

        /// <summary>
        /// Returns the number of bytes currently written in this packet.
        /// </summary>
        public int Size => Buffer.Size - HeaderSize;

        /// <summary>
        /// Returns how many more bytes can be written to this packet. If positive value is
        /// returned, that many bytes are still left. If zero is returned, packet is full.
        /// If negative value is returned, packet must be sent over a channel that performs
        /// fragmentation and reassembly.
        /// </summary>
        public int UnwrittenBytes => MaxSize - Size;

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
        public static Packet Get(byte channelId) => Get(HeaderType.Data).Write(channelId).Write(ChannelHeaderFill);

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
        /// Gets or sets byte at specified index. Index must be in range from 0 (inclusive) to <see cref="Size"/> (exclusive).
        /// </summary>
        public byte this[int index]
        {
            get
            {
                if (index < 0)
                    throw new InvalidOperationException($"Cannot get byte at index {index}.");
                
                if (index >= Size)
                    throw new InvalidOperationException($"Cannot get byte at index {index} as it is equal to or exceeds current packet size of {Size}.");
                
                return Buffer.Bytes[HeaderSize + index];
            }
            set
            {
                if (index < 0)
                    throw new InvalidOperationException($"Cannot set byte at index {index}.");
                
                if (index >= Size)
                    throw new InvalidOperationException($"Cannot set byte at index {index} as it is equal to or exceeds current packet size of {Size}.");

                Buffer.Bytes[HeaderSize + index] = value;
            }
        }

        /// <summary>
        /// Writes a <see cref="string"/> to this packet (using encoding defined by <see cref="Encoding"/>).
        /// </summary>
        public Packet Write(string value)
        {
            if (value is null)
                throw new InvalidOperationException("Cannot write null string to a packet.");
            
            return value.Length == 0 ? Write<byte>(0) : WriteArray(Encoding.GetBytes(value));
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
        public Packet WriteArray<T>(T[] array, bool writeLength = true) where T : unmanaged
        {
            if (array is null)
                throw new InvalidOperationException("Cannot write null array to a packet.");
            
            return WriteArray(array, start: 0, length: array.Length, writeLength);
        }

        /// <summary>
        /// Writes the portion of an array to this packet. 
        /// </summary>
        /// <param name="array">Array to write.</param>
        /// <param name="start">Index in the array from which to start writing elements.</param>
        /// <param name="length">Number of elements to write.</param>
        /// <param name="writeLength">If <c>true</c>, length will be written before elements, otherwise only elements will be written.</param>
        public Packet WriteArray<T>(T[] array, int start, int length, bool writeLength = true) where T : unmanaged
        {
            if (array is null)
                throw new InvalidOperationException("Cannot write null array to a packet.");
            
            if (writeLength)
                Buffer.WriteVarInt(length);
            
            Buffer.WriteArray(array, start, length);
            return this;
        }

        /// <summary>
        /// Returns previously borrowed <see cref="Link.Buffer"/> to the pool, making it reusable again.
        /// </summary>
        public void Return() => Buffer.Return();
    }
}
