using System.Net;

namespace Networking.Transport
{
    /// <summary>
    /// Represents an object that consumes the received packet.
    /// </summary>
    public interface IPacketHandler
    {
        /// <summary>
        /// Handles the received packet on the main thread.
        /// </summary>
        /// <param name="packet">Packet that was received.</param>
        /// <param name="senderEndPoint">End point of the packet sender.</param>
        void Handle(Packet packet, EndPoint senderEndPoint);
    }
}
