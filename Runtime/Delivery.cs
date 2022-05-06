namespace Link
{
    /// <summary>
    /// Defines the way data should be delivered to the remote destination.
    /// </summary>
    public enum Delivery : byte
    {
        /// <summary>
        /// Fire and forget delivery method; packets might be lost on the way, can be duplicated and can arrive out of order.
        /// <br/><br/>
        /// Useful for inspecting network. Example: ping packets when trying to calculate round-trip-time and packet loss.
        /// </summary>
        Unreliable = 240,

        /// <summary>
        /// Sequence number is attached to each packet. Packets can be lost, but won't be duplicated and will preserve order.
        /// <br/><br/>
        /// When packet is received, it is processed in the following manner: If received sequence number is greater than last
        /// received sequence number, packet is processed (and last received sequence number is set to received), otherwise it
        /// is discarded. Perfect for rapidly changing state where only the latest state is important.
        /// </summary>
        Sequenced,

        /// <summary>
        /// Each packet is guaranteed to be delivered, won't be duplicated and will arrive in order.
        /// <br/><br/>
        /// This is an expensive delivery method as every packet needs to be acknowledged by the receiving end-point.
        /// Any data that must be delivered reliably should use this delivery method (example: chat messages).
        /// </summary>
        Reliable,
    }
}
