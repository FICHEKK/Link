using System;

namespace Networking.Transport
{
    /// <summary>
    /// Parameters used to create artificial network conditions.
    /// </summary>
    public class SimulationSettings
    {
        private float _packetLoss;

        /// <summary>
        /// Defines the probability of a packet being lost.
        /// This property should only be used only for testing purposes.
        /// </summary>
        /// <remarks>Value must be in range from 0 to 1.</remarks>
        public float PacketLoss
        {
            get => _packetLoss;
            set
            {
                if (value is < 0 or > 1)
                    throw new ArgumentOutOfRangeException(nameof(PacketLoss), "Packet loss must be in range from 0 to 1.");

                _packetLoss = value;
            }
        }
    }
}
