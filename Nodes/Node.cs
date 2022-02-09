using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Networking.Attributes;
using Networking.Core;
using Networking.Exceptions;
using UnityEngine;

namespace Networking.Transport.Nodes
{
    /// <summary>
    /// Represents a network node - a fundamental building block
    /// of any network graph that can send and receive data.
    /// </summary>
    public abstract class Node
    {
        private static readonly EndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private readonly Dictionary<ushort, Action<PacketReader, EndPoint>> _packetIdToPacketHandler = new Dictionary<ushort, Action<PacketReader, EndPoint>>();
        private readonly Queue<(Packet packet, EndPoint senderEndPoint)> _pendingPackets = new Queue<(Packet, EndPoint)>();
        private readonly byte[] _receiveBuffer = new byte[4096];
        private Socket _socket;

        /// <summary>
        /// Returns true if this node is currently listening for incoming packets.
        /// </summary>
        public bool IsListening => _socket != null;

        /// <summary>
        /// Starts listening for incoming packets.
        /// </summary>
        /// <param name="port">Port to listen on.</param>
        protected void StartListening(int port)
        {
            if (IsListening) throw new InvalidOperationException("Could not start listening as node is already listening.");

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));
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
                if (_socket == null) return;

                var senderEndPoint = AnyEndPoint;
                var bytesReceived = _socket.EndReceiveFrom(asyncResult, ref senderEndPoint);

                if (bytesReceived > 0)
                {
                    var packetId = _receiveBuffer.ReadUnsignedShort(offset: 0);
                    var packet = Packet.Get(packetId);

                    Array.Copy(_receiveBuffer, packet.Buffer, bytesReceived);
                    lock (_pendingPackets) _pendingPackets.Enqueue((packet, senderEndPoint));
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

        /// <summary>
        /// Sends outgoing packet to the specified end-point.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        /// <param name="receiverEndPoint">Where to send the packet to.</param>
        public void Send(Packet packet, EndPoint receiverEndPoint)
        {
            SendWithoutReturningToPool(packet, receiverEndPoint);
            packet.Return();
        }

        protected void SendWithoutReturningToPool(Packet packet, EndPoint receiverEndPoint)
        {
            var buffer = packet.Buffer;
            var size = packet.Writer.WritePosition;
            _socket.BeginSendTo(buffer, offset: 0, size, SocketFlags.None, receiverEndPoint, callback: null, state: null);
        }

        /// <summary>
        /// Stop listening for incoming packets.
        /// </summary>
        protected void StopListening()
        {
            if (!IsListening) throw new InvalidOperationException("Could not stop listening as node is already not listening.");

            _socket.Dispose();
            _socket = null;
        }

        /// <summary>
        /// Handles all of the packets that have been enqueued since last time this method was called.
        /// </summary>
        public void HandlePendingPackets()
        {
            lock (_pendingPackets)
            {
                while (_pendingPackets.Count > 0)
                {
                    var (packet, senderEndPoint) = _pendingPackets.Dequeue();

                    if (!_packetIdToPacketHandler.TryGetValue(packet.Id, out var packetHandler))
                    {
                        Debug.LogError($"Could not handle packet (ID = {packet.Id}) as it does not have a registered handler.");
                        packet.Return();
                        continue;
                    }

                    packetHandler(packet.Reader, senderEndPoint);
                    packet.Return();
                }
            }
        }

        protected void RegisterPacketHandlersOfType<T>() where T : PacketHandlerAttribute
        {
            foreach (var assembly in GetType().Assembly.GetAssembliesThatReferenceThisAssembly(includeSelf: true))
            {
                foreach (var type in assembly.GetTypes())
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                    {
                        var attribute = method.GetCustomAttribute<T>();
                        if (attribute == null) return;

                        if (!method.IsStatic)
                        {
                            var line1 = $"Could not register non-static method {method.FullyQualifiedName().B().C(Color.cyan)}.";
                            var line2 = $"Packet handlers registered with {nameof(PacketHandlerAttribute).B().C(Color.yellow)} must be static methods.";
                            var line3 = $"For instance methods, use {nameof(RegisterPacketHandler).B().C(Color.cyan)} instead.";
                            throw new PacketHandlerNotStaticException(line1.NewLine() + line2.NewLine() + line3.NewLine());
                        }

                        var requiredType = typeof(Action<PacketReader, EndPoint>);
                        var packetHandler = Delegate.CreateDelegate(requiredType, method, throwOnBindFailure: false);

                        if (packetHandler == null)
                        {
                            var line1 = $"Method {method.FullyQualifiedName().B().C(Color.cyan)} does not match required packet handler signature.";
                            var line2 = $"Required signature must match delegate of type {requiredType.ToString().B().C(Color.cyan)}.";
                            throw new PacketHandlerSignatureMismatchException(line1.NewLine() + line2.NewLine());
                        }

                        RegisterPacketHandler(attribute.PacketId, (Action<PacketReader, EndPoint>) packetHandler);
                    }
                }
            }
        }

        public void RegisterPacketHandler(ushort packetId, Action<PacketReader, EndPoint> packetHandler)
        {
            if (_packetIdToPacketHandler.TryGetValue(packetId, out var existingPacketHandler))
            {
                var line1 = $"Packet handler collision for packet with {$"ID = {packetId}".B().C(Color.white)}, on {GetType().ToString().B().C(Color.yellow)}.";
                var line2 = $"Method 1: {packetHandler.Method.FullyQualifiedName().B().C(Color.cyan)}";
                var line3 = $"Method 2: {existingPacketHandler.Method.FullyQualifiedName().B().C(Color.cyan)}";
                throw new PacketHandlerCollisionException(line1.NewLine() + line2.NewLine() + line3.NewLine());
            }

            _packetIdToPacketHandler.Add(packetId, packetHandler);

            var header = $"[Packet Handler on {GetType().ToString().C(Color.yellow)}]".B().C(Color.green);
            var body = $"Registered {packetHandler.Method.FullyQualifiedName().B().C(Color.cyan)}, {$"ID = {packetId}".B().C(Color.white)}.";
            Debug.Log($"{header} - {body}");
        }
    }
}
