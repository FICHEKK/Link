using System;

namespace Networking.Transport
{
    /// <summary>
    /// Parameters used to create artificial network conditions.
    /// </summary>
    public class SimulationSettings
    {
        private float _packetLoss;
        private int _minLatency;
        private int _maxLatency;

        /// <summary>
        /// Defines the probability of a packet being lost.
        /// This property should only be used only for testing purposes.
        /// </summary>
        /// <remarks>Value must be in range from 0 to 1.</remarks>
        public float PacketLoss
        {
            get => _packetLoss;
            set => _packetLoss = value is >= 0 and <= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(PacketLoss), "Packet loss must be in range from 0 to 1.");
        }

        /// <summary>
        /// Minimum additional delay (in ms) before processing received packet.
        /// </summary>
        public int MinLatency
        {
            get => _minLatency;
            set => _minLatency = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MinLatency), "Min latency must be a non-negative value.");
        }

        /// <summary>
        /// Maximum additional delay (in ms) before processing received packet.
        /// </summary>
        public int MaxLatency
        {
            get => _maxLatency;
            set => _maxLatency = value >= 0
                ? value
                : throw new ArgumentOutOfRangeException(nameof(MaxLatency), "Max latency must be a non-negative value.");
        }
    }
}
