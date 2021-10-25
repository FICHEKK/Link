using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Networking.Packets;
using UnityEngine;
using Random = System.Random;

namespace Networking.Transport
{
    public class NetworkSocket : IDisposable
    {
        private static readonly EndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private readonly byte[] _receiveBuffer = new byte[1024];
        private readonly Socket _socket;
        private readonly Random _random = new Random();
        private readonly Action<byte[], EndPoint> _datagramHandler;

        public float PacketLossProbability { get; set; }
        public int MinLatency { get; set; }
        public int MaxLatency { get; set; }

        public NetworkSocket(Socket socket, Action<byte[], EndPoint> datagramHandler, float packetLossProbability = 0, int minLatency = 0, int maxLatency = 0)
        {
            if (packetLossProbability < 0 || packetLossProbability > 1)
                throw new ArgumentException($"Packet loss probability must be a value in range from 0 to 1. Provided value: {packetLossProbability}");

            if (minLatency < 0)
                throw new ArgumentException($"Minimum latency must be a positive value. Provided value: {minLatency}");

            if (minLatency > maxLatency)
                throw new ArgumentException("Maximum latency must be greater than minimum latency.");

            PacketLossProbability = packetLossProbability;
            MinLatency = minLatency;
            MaxLatency = maxLatency;

            _socket = socket ?? throw new NullReferenceException(nameof(socket));
            _datagramHandler = datagramHandler ?? throw new NullReferenceException(nameof(datagramHandler));

            ReceiveFromAnySource();
        }

        private void ReceiveFromAnySource()
        {
            var anyEndPoint = AnyEndPoint;
            _socket.BeginReceiveFrom(_receiveBuffer, ref anyEndPoint, HandleReceive);
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

        public void Send(Packet packet, EndPoint receiverEndPoint) =>
            packet.Send(_socket, receiverEndPoint);

        public void Dispose() =>
            _socket.Dispose();
    }
}
