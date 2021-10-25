using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using Random = System.Random;

namespace Networking.Transport
{
    public class NetworkSocket : IDisposable
    {
        private static readonly EndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private readonly Socket _socket;
        private readonly Random _random = new Random();
        private readonly byte[] _receiveBuffer = new byte[4096];
        private readonly Action<byte[], EndPoint> _datagramHandler;

        public float PacketLossProbability { get; set; }
        public int MinLatency { get; set; }
        public int MaxLatency { get; set; }

        public NetworkSocket(Socket socket, Action<byte[], EndPoint> datagramHandler)
        {
            _socket = socket ?? throw new NullReferenceException(nameof(socket));
            _datagramHandler = datagramHandler ?? throw new NullReferenceException(nameof(datagramHandler));
            ReceiveFromAnySource();
        }

        private void ReceiveFromAnySource()
        {
            var anyEndPoint = AnyEndPoint;
            _socket.BeginReceiveFrom(_receiveBuffer, offset: 0, _receiveBuffer.Length, SocketFlags.None, ref anyEndPoint, HandleReceive, state: null);
        }

        private void HandleReceive(IAsyncResult asyncResult)
        {
            try
            {
                var senderEndPoint = AnyEndPoint;
                var bytesReceived = _socket.EndReceiveFrom(asyncResult, ref senderEndPoint);

                if (bytesReceived > 0 && (PacketLossProbability == 0 || _random.NextDouble() >= PacketLossProbability))
                {
                    var datagram = new byte[bytesReceived];
                    Array.Copy(_receiveBuffer, datagram, datagram.Length);
                    HandleDatagram(datagram, senderEndPoint);
                }

                ReceiveFromAnySource();
            }
            catch (ObjectDisposedException)
            {
                Debug.Log("Socket has been disposed.");
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"Socket exception {e.ErrorCode}, {e.SocketErrorCode}: {e.Message}");
                ReceiveFromAnySource();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Unexpected exception: {e}");
                ReceiveFromAnySource();
            }
        }

        private async void HandleDatagram(byte[] datagram, EndPoint senderEndPoint)
        {
            if (MaxLatency > 0) await Task.Delay(_random.Next(MinLatency, MaxLatency + 1));
            _datagramHandler(datagram, senderEndPoint);
        }

        public void Send(byte[] datagram, int offset, int length, EndPoint receiverEndPoint) =>
            _socket.BeginSendTo(datagram, offset, length, SocketFlags.None, receiverEndPoint, callback: null, state: null);

        public void Dispose() =>
            _socket.Dispose();
    }
}
