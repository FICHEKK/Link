using System;
using System.Diagnostics;
using System.Threading;

namespace Networking.Transport
{
    /// <summary>
    /// Component that measures ping (round-trip time) for the given connection.
    /// </summary>
    public class PingMeasurer : IDisposable
    {
        /// <summary>
        /// Returns round-trip time with applied exponential smoothing.
        /// </summary>
        public double SmoothRoundTripTime { get; private set; }

        /// <summary>
        /// Returns the most recently calculated round-trip time, in milliseconds.
        /// </summary>
        public double RoundTripTime { get; private set; }

        private readonly Connection _connection;
        private readonly Stopwatch _rttStopwatch;
        private readonly Timer _sendPingTimer;
        private readonly int _periodDuration;
        private readonly int _timeoutDuration;
        private readonly double _smoothingFactor;

        private ushort _lastSentPingId;
        private ushort _lastReceivedPingId;
        private DateTime _lastPingResponseTime;

        /// <summary>
        /// Constructs a new ping measurer with specified settings.
        /// </summary>
        /// <param name="connection">Connection for which ping should be measured.</param>
        /// <param name="periodDuration">Duration between two consecutive ping packets, in milliseconds.</param>
        /// <param name="timeoutDuration">If ping response is not received for this duration (in milliseconds), connection is going to get timed-out.</param>
        /// <param name="smoothingFactor">Factor used to apply exponential smoothing in order to calculate the value of <see cref="SmoothRoundTripTime"/>.</param>
        public PingMeasurer(Connection connection, int periodDuration = 1000, int timeoutDuration = 10_000, double smoothingFactor = 0.618)
        {
            _connection = connection;
            _rttStopwatch = new Stopwatch();
            _sendPingTimer = new Timer(_ => SendPing());

            _periodDuration = periodDuration;
            _timeoutDuration = timeoutDuration;
            _smoothingFactor = smoothingFactor;
        }

        public void StartMeasuring()
        {
            _lastPingResponseTime = DateTime.UtcNow;
            _sendPingTimer.Change(dueTime: 0, period: _periodDuration);
        }

        public void StopMeasuring()
        {
            _sendPingTimer.Change(dueTime: Timeout.Infinite, period: Timeout.Infinite);
        }

        private void SendPing()
        {
            if ((DateTime.UtcNow - _lastPingResponseTime).TotalMilliseconds > _timeoutDuration)
            {
                _connection.Timeout();
                Log.Info($"Connection timed-out: Valid ping response was not received in over {_timeoutDuration} ms.");
                return;
            }

            var pingPacket = Packet.Get(HeaderType.Ping);
            pingPacket.Writer.Write(++_lastSentPingId);
            _connection.Node.Send(pingPacket, _connection.RemoteEndPoint);
            _rttStopwatch.Restart();
        }

        internal void ReceivePing(byte[] datagram)
        {
            var pongPacket = Packet.Get(HeaderType.Pong);
            pongPacket.Writer.Write(datagram.Read<ushort>(offset: 1));
            _connection.Node.Send(pongPacket, _connection.RemoteEndPoint);
        }

        internal void ReceivePong(byte[] datagram)
        {
            var id = datagram.Read<ushort>(offset: 1);
            if (id <= _lastReceivedPingId) return;

            _lastReceivedPingId = id;
            _lastPingResponseTime = DateTime.UtcNow;

            RoundTripTime = (_lastSentPingId - _lastReceivedPingId) * _periodDuration + _rttStopwatch.Elapsed.TotalMilliseconds;
            SmoothRoundTripTime = _smoothingFactor * RoundTripTime + (1 - _smoothingFactor) * SmoothRoundTripTime;
        }

        public void Dispose() => _sendPingTimer.Dispose();
    }
}
