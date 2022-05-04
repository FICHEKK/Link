using System.Collections.Generic;
using System.Threading;

namespace Link.Channels
{
    /// <summary>
    /// Represents a packet that hasn't been acknowledged by the receiver and automatically
    /// performs retransmissions if not acknowledged for a certain period of time.
    /// </summary>
    /// <remarks>This class is thread-safe.</remarks>
    internal class PendingPacket
    {
        /// <summary>
        /// Collection of reusable pending packet instances used to avoid frequent memory allocations.
        /// </summary>
        private static readonly Queue<PendingPacket> PendingPacketPool = new();

        private readonly Timer _resendTimer;
        private readonly object _lock = new();

        private Buffer _packet;
        private ReliableChannel _reliableChannel;
        private int _resendAttempts;
        private double _backoff;

        public static PendingPacket Get(Packet packet, ReliableChannel reliableChannel)
        {
            // It is crucial to make a copy of provided packet for multiple reasons:
            // 1. If same packet is sent to multiple end-points, it would get returned multiple times.
            // 2. We can immediately return original packet to pool as usual, making logic consistent.
            var pendingPacket = Get();
            pendingPacket._packet = Buffer.Copy(packet.Buffer);
            pendingPacket._reliableChannel = reliableChannel;
            pendingPacket._resendAttempts = 0;
            pendingPacket._backoff = 1;

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

                if (_resendAttempts < _reliableChannel.MaxResendAttempts)
                {
                    _reliableChannel.ResendPacket(new Packet(_packet));
                    _resendAttempts++;
                    ScheduleResend();
                }
                else
                {
                    _reliableChannel.HandleLostPacket(new Packet(_packet));
                    Acknowledge();
                }
            }
        }

        private void ScheduleResend()
        {
            var baseDelay = _reliableChannel.Connection.SmoothRoundTripTime + 4 * _reliableChannel.Connection.RoundTripTimeDeviation;
            var resendDelay = (int) (baseDelay * _backoff);
            _backoff *= _reliableChannel.BackoffFactor;

            if (resendDelay < _reliableChannel.MinResendDelay)
                resendDelay = _reliableChannel.MinResendDelay;

            _resendTimer.Change(dueTime: resendDelay, period: Timeout.Infinite);
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

                // Lose references so they can be garbage collected.
                _packet = null;
                _reliableChannel = null;

                lock (PendingPacketPool) PendingPacketPool.Enqueue(this);
            }
        }
    }
}
