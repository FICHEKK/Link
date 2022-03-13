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
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(MinLatency), "Min latency must be a non-negative value.");

                if (value > _maxLatency)
                    throw new ArgumentOutOfRangeException(nameof(MinLatency), "Min latency cannot be greater than max latency.");

                _minLatency = value;
            }
        }

        /// <summary>
        /// Maximum additional delay (in ms) before processing received packet.
        /// </summary>
        public int MaxLatency
        {
            get => _maxLatency;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(MaxLatency), "Max latency must be a non-negative value.");

                if (value < _minLatency)
                    throw new ArgumentOutOfRangeException(nameof(MaxLatency), "Max latency cannot be less than min latency.");

                _maxLatency = value;
            }
        }
    }
}
