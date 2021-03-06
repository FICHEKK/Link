using System;
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

        private Buffer? _buffer;
        private ReliableChannel? _reliableChannel;
        private int _resendAttempts;
        private double _backoff;

        public static PendingPacket Get(Packet packet, ReliableChannel reliableChannel)
        {
            var pendingPacket = Get();
            pendingPacket._buffer = Buffer.Copy(packet.Buffer);
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
            // Round-trip time hasn't been measured yet - do not resend.
            if (_reliableChannel!.BaseResendDelay < 0)
            {
                _backoff /= _reliableChannel.BackoffFactor;
                ScheduleResend();
                return;
            }
            
            // At the time we are resending, other thread could be acknowledging.
            lock (_lock)
            {
                // Other thread has already acknowledged this packet.
                if (_buffer is null) return;

                if (_resendAttempts >= _reliableChannel.MaxResendAttempts)
                {
                    _reliableChannel.HandleLostPacket(new Packet(_buffer));
                    Acknowledge();
                    return;
                }

                if (!_reliableChannel.ResendPacket(new Packet(_buffer)))
                {
                    // Channel has been closed, this packet's job is done.
                    Acknowledge();
                    return;
                }

                _resendAttempts++;
                ScheduleResend();
            }
        }

        private void ScheduleResend()
        {
            var resendDelay = Math.Max(_reliableChannel!.BaseResendDelay, _reliableChannel.MinResendDelay) * _backoff;
            _backoff *= _reliableChannel.BackoffFactor;

            _resendTimer.Change(dueTime: (int) resendDelay, period: Timeout.Infinite);
        }

        public void Acknowledge()
        {
            // Do not allow multiple threads to acknowledge at the same
            // time as that would return the same instance to the pool.
            lock (_lock)
            {
                // Other thread has already acknowledged this packet.
                if (_buffer is null) return;

                _resendTimer.Change(dueTime: Timeout.Infinite, period: Timeout.Infinite);
                _buffer.Return();

                // Lose references so they can be garbage collected.
                _buffer = null;
                _reliableChannel = null;

                lock (PendingPacketPool) PendingPacketPool.Enqueue(this);
            }
        }
    }
}
