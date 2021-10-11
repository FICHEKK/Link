using System;
using System.Collections.Concurrent;
using System.Net;
using UnityEngine;
using Random = System.Random;

namespace Networking.Transport
{
    public class NetworkSocketWithLatency : NetworkSocket
    {
        [SerializeField, Range(0, 1), Tooltip("Defines the probability of a client packet being lost.")]
        private float packetLossProbability;

        [SerializeField, Min(0), Tooltip("Adds additional delay (in ms) before sending the response packet.")]
        private int latency;

        private readonly ConcurrentQueue<Request> _requests = new ConcurrentQueue<Request>();
        private readonly Random _random = new Random();

        protected override void HandlePacket(Packet packet, EndPoint senderEndPoint)
        {
            if (_random.NextDouble() < packetLossProbability)
            {
                Debug.Log($"Packet of type {packet.Type} was lost.");
                return;
            }

            var arrivalTimeWithLatency = DateTime.UtcNow.AddMilliseconds(latency);
            _requests.Enqueue(new Request(packet, senderEndPoint, arrivalTimeWithLatency));
        }

        private void Update()
        {
            var now = DateTime.UtcNow;
            var requestCount = _requests.Count;

            for (var i = 0; i < requestCount; i++)
            {
                if (!_requests.TryDequeue(out var request)) continue;

                if (now < request.ArrivalTime)
                {
                    _requests.Enqueue(request);
                    continue;
                }

                base.HandlePacket(request.Packet, request.SenderEndPoint);
            }
        }

        private readonly struct Request
        {
            public Packet Packet { get; }
            public EndPoint SenderEndPoint { get; }
            public DateTime ArrivalTime { get; }

            public Request(Packet packet, EndPoint senderEndPoint, DateTime arrivalTime)
            {
                Packet = packet;
                SenderEndPoint = senderEndPoint;
                ArrivalTime = arrivalTime;
            }
        }
    }
}
