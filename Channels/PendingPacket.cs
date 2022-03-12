using System.Collections.Generic;
using System.Threading;

namespace Networking.Transport.Channels
{
    /// <summary>
    /// Represents a packet that hasn't been acknowledged by the receiver and automatically
    /// performs retransmissions if not acknowledged for a certain period of time.
    /// </summary>
    /// <remarks>This class is thread-safe.</remarks>
    public class PendingPacket
    {
        /// <summary>
        /// Minimum possible time duration before resending the packet, in milliseconds.
        /// </summary>
        private const int MinResendDelay = 1;

        /// <summary>
        /// Maximum number of resend attempts before deeming the packet as lost.
        /// </summary>
        private const int MaxResendAttempts = 15;

        /// <summary>
        /// Collection of reusable pending packet instances used to avoid frequent memory allocations.
        /// </summary>
        private static readonly Queue<PendingPacket> PendingPacketPool = new();

        private readonly Timer _resendTimer;
        private readonly object _lock = new();

        private Packet _packet;
        private IReliableChannel _reliableChannel;
        private int _resendAttempts;

        public static PendingPacket Get(Packet packet, IReliableChannel reliableChannel)
        {
            var pendingPacket = Get();
            pendingPacket._packet = packet;
            pendingPacket._reliableChannel = reliableChannel;
            pendingPacket.ScheduleResend();
            return pendingPacket;
        }

        private static PendingPacket Get()
        {
            lock (PendingPacketPool)
                if (PendingPacketPool.Count > 0)
                    return PendingPacketPool.Dequeue();

            return new PendingPacket();
        }

        private PendingPacket() =>
            _resendTimer = new Timer(_ => Resend());

        private void Resend()
        {
            // At the time we are resending, other thread could be acknowledging.
            lock (_lock)
            {
                // Other thread has already acknowledged this packet.
                if (_packet is null) return;

                if (_resendAttempts < MaxResendAttempts)
                {
                    _reliableChannel.ResendPacket(_packet);
                    _resendAttempts++;
                    ScheduleResend();
                }
                else
                {
                    _reliableChannel.HandleLostPacket(_packet);
                    Acknowledge();
                }
            }
        }

        private void ScheduleResend()
        {
            var resendDelayDuration = (int) (_reliableChannel.RoundTripTime * 2);

            if (resendDelayDuration < MinResendDelay)
                resendDelayDuration = MinResendDelay;

            _resendTimer.Change(dueTime: resendDelayDuration, period: Timeout.Infinite);
        }

        public void Acknowledge()
        {
            // Do not allow multiple threads to acknowledge at the same
            // time as that would return the same instance to the pool.
            lock (_lock)
            {
                // Other thread has already acknowledged this packet.
                if (_packet is null) return;

                _resendTimer.Change(dueTime: Timeout.Infinite, period: Timeout.Infinite);
                _packet.Return();

                _packet = null;
                _reliableChannel = null;
                _resendAttempts = 0;

                lock (PendingPacketPool) PendingPacketPool.Enqueue(this);
            }
        }
    }
}
