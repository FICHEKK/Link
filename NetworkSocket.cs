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

        private readonly byte[] _receiveBuffer = new byte[1024];
        private readonly Socket _socket;
        private readonly Random _random = new Random();
        private readonly IPacketHandler _packetHandler;

        public float PacketLossProbability { get; set; }
        public int Latency { get; set; }

        public NetworkSocket(Socket socket, IPacketHandler packetHandler, float packetLossProbability = 0, int latency = 0)
        {
            if (packetLossProbability < 0 || packetLossProbability > 1)
                throw new ArgumentException($"Packet loss probability must be a value in range from 0 to 1. Provided value: {packetLossProbability}");

            if (latency < 0)
                throw new ArgumentException($"Latency must be a positive value. Provided value: {latency}");

            PacketLossProbability = packetLossProbability;
            Latency = latency;

            _socket = socket ?? throw new NullReferenceException(nameof(socket));
            _packetHandler = packetHandler ?? throw new NullReferenceException(nameof(packetHandler));

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

                if (bytesReceived > 0)
                {
                    var packet = Packet.OfBytes(_receiveBuffer, bytesReceived);
                    HandlePacket(packet, senderEndPoint);
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

        private async void HandlePacket(Packet packet, EndPoint senderEndPoint)
        {
            if (PacketLossProbability > 0 && _random.NextDouble() < PacketLossProbability)
            {
                Debug.Log($"Packet of type {packet.Type} was lost.");
                return;
            }

            if (Latency > 0) await Task.Delay(Latency);

            _packetHandler.Handle(packet, senderEndPoint);
        }

        public void Send(Packet packet, EndPoint receiverEndPoint) =>
            packet.Send(_socket, receiverEndPoint);

        public void Dispose() =>
            _socket.Dispose();
    }
}
