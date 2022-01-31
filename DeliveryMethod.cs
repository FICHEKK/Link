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
        /// </summary>
        public static DeliveryMethod Unreliable = new DeliveryMethod(id: 0, headerSizeInBytes: 1);

        /// <summary>
        /// Sequence number is attached to each packet. Packets can be lost, but won't be duplicated. When packet is received,
        /// it is processed in the following manner: If received sequence number is equal or greater than expected sequence
        /// number, packet is processed (and expected sequence number is set to received + 1), otherwise it is discarded.
        /// Perfect for rapidly changing state where only the latest state is important.
        /// </summary>
        public static DeliveryMethod UnreliableSequenced = new DeliveryMethod(id: 1, headerSizeInBytes: 3);

        /// <summary>
        /// Each packet is guaranteed to be delivered (unless the connection is faulty), won't be duplicated and will arrive in order.
        /// This is the most expensive delivery method as every packet needs to be acknowledged by the receiving end-point.
        /// Any data that must be delivered and be in order should use this delivery method (example: chat messages).
        /// </summary>
        // TODO - Define actual header size of reliable delivery method.
        public static DeliveryMethod Reliable = new DeliveryMethod(id: 2, headerSizeInBytes: -1);

        public byte Id { get; }
        public int HeaderSizeInBytes { get; }

        private DeliveryMethod(byte id, int headerSizeInBytes)
        {
            Id = id;
            HeaderSizeInBytes = headerSizeInBytes;
        }
    }
}
