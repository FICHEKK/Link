namespace Networking.Transport
{
    /// <summary>
    /// Defines how the data should be delivered to the remote destination.
    /// </summary>
    public sealed class DeliveryMethod
    {
        /// <summary>
        /// Fire and forget delivery; packet might be lost on the way, can be duplicated and doesn't guarantee ordering.
        /// Useful for inspecting network. Example: ping packets when trying to calculate round-trip-time and packet loss.
        /// <br/><br/>
        /// Header size is 1 byte: delivery method ID.
        /// </summary>
        public static readonly DeliveryMethod Unreliable = new(id: (byte) HeaderType.UnreliableData, headerSizeInBytes: 1);

        /// <summary>
        /// Sequence number is attached to each packet. Packets can be lost, but won't be duplicated and will preserve order.
        /// When packet is received, it is processed in the following manner: If received sequence number is greater than last
        /// received sequence number, packet is processed (and last received sequence number is set to received), otherwise it
        /// is discarded. Perfect for rapidly changing state where only the latest state is important.
        /// <br/><br/>
        /// Header size is 3 bytes (1+2): delivery method ID, sequence number.
        /// </summary>
        public static readonly DeliveryMethod Sequenced = new(id: (byte) HeaderType.SequencedData, headerSizeInBytes: 3);

        /// <summary>
        /// Each packet is guaranteed to be delivered (unless the connection is faulty), won't be duplicated and will arrive in order.
        /// This is the most expensive delivery method as every packet needs to be acknowledged by the receiving end-point.
        /// Any data that must be delivered and be in order should use this delivery method (example: chat messages).
        /// <br/><br/>
        /// Header size is 9 bytes (1+2+2+4): delivery method ID, local sequence number, acknowledge sequence number, acknowledge bit-field.
        /// </summary>
        public static readonly DeliveryMethod Reliable = new(id: (byte) HeaderType.ReliableData, headerSizeInBytes: 9);

        public byte Id { get; }
        public int HeaderSizeInBytes { get; }

        private DeliveryMethod(byte id, int headerSizeInBytes)
        {
            Id = id;
            HeaderSizeInBytes = headerSizeInBytes;
        }
    }
}
