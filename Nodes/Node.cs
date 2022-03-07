using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Networking.Exceptions;

namespace Networking.Transport.Nodes
{
    /// <summary>
    /// Represents a network node - a fundamental building block of any network graph that can send and receive data.
    /// </summary>
    public abstract class Node
    {
        private static readonly EndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        /// <summary>
        /// Returns packet handlers registered for this network node.
        /// </summary>
        public IReadOnlyDictionary<ushort, Action<PacketReader, EndPoint>> PacketIdToPacketHandler => _packetIdToPacketHandler;

        /// <summary>
        /// Returns port on which this node is listening on, or <c>-1</c> if not currently listening.
        /// </summary>
        public int Port => IsListening ? ((IPEndPoint) _socket.LocalEndPoint).Port : -1;

        /// <summary>
        /// Returns true if this node is currently listening for incoming packets.
        /// </summary>
        public bool IsListening => _socket != null;

        private readonly Dictionary<ushort, Action<PacketReader, EndPoint>> _packetIdToPacketHandler = new();
        private readonly Queue<(Packet packet, EndPoint senderEndPoint)> _pendingPackets = new();
        private readonly Queue<Action> _mainThreadActions = new();
        private readonly byte[] _receiveBuffer = new byte[4096];
        private Socket _socket;

        /// <summary>
        /// Starts listening for incoming packets.
        /// </summary>
        /// <param name="port">Port to listen on.</param>
        protected void StartListening(int port)
        {
            if (IsListening) throw Error.NodeAlreadyListening("Could not start listening as node is already listening.");

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, port));

            new Thread(Listen)
            {
                Name = $"{GetType()}::{nameof(Listen)}",
                IsBackground = true
            }.Start();
        }

        private void Listen()
        {
            Log.Info($"Starting thread '{Thread.CurrentThread.Name}'...");

            while (true)
            {
                try
                {
                    if (_socket is null) break;

                    var senderEndPoint = AnyEndPoint;
                    var bytesReceived = _socket.ReceiveFrom(_receiveBuffer, ref senderEndPoint);
                    if (bytesReceived == 0) continue;

                    var packet = Receive(_receiveBuffer, bytesReceived, senderEndPoint);
                    if (packet is null) continue;

                    lock (_pendingPackets) _pendingPackets.Enqueue((packet, senderEndPoint));
                }
                catch (ObjectDisposedException)
                {
                    Log.Info("Socket has been disposed.");
                    break;
                }
                catch (SocketException e)
                {
                    // Socket has been forcefully disposed while receiving.
                    if (e.SocketErrorCode == SocketError.Interrupted) break;

                    // This is a known ICMP message received when destination port is unreachable.
                    // This occurs when remote end-point disconnects without sending the disconnect
                    // packet (or if disconnect packet did not arrive). This error does not need to
                    // be handled as the connection will get cleaned automatically once it times out.
                    if (e.SocketErrorCode == SocketError.ConnectionReset) continue;

                    Log.Warning($"Socket exception {e.ErrorCode}, {e.SocketErrorCode}: {e.Message}");
                }
                catch (Exception e)
                {
                    Log.Error($"Unexpected exception: {e}");
                }
            }

            Log.Info($"Stopping thread '{Thread.CurrentThread.Name}'...");
        }

        /// <summary>
        /// Processes received bytes and returns packet instance if bytes represent a data packet.
        /// </summary>
        /// <param name="datagram">Buffer containing bytes being processed.</param>
        /// <param name="bytesReceived">How many bytes were received.</param>
        /// <param name="senderEndPoint">Specifies from where bytes came from.</param>
        /// <returns>Packet instance if bytes represent a data packet, null otherwise.</returns>
        protected abstract Packet Receive(byte[] datagram, int bytesReceived, EndPoint senderEndPoint);

        /// <summary>
        /// Sends outgoing packet to the specified end-point.
        /// </summary>
        /// <param name="packet">Packet being sent.</param>
        /// <param name="receiverEndPoint">Where to send the packet to.</param>
        /// <param name="returnPacketToPool">Whether given packet should be returned to pool after sending.</param>
        public void Send(Packet packet, EndPoint receiverEndPoint, bool returnPacketToPool = true)
        {
            _socket.SendTo(packet.Buffer, offset: 0, size: packet.Writer.WritePosition, SocketFlags.None, receiverEndPoint);
            if (returnPacketToPool) packet.Return();
        }

        /// <summary>
        /// Stop listening for incoming packets.
        /// </summary>
        protected void StopListening()
        {
            if (!IsListening) throw Error.NodeAlreadyNotListening("Could not stop listening as node is already not listening.");

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
                    var packetId = packet.Reader.Read<ushort>();

                    if (!_packetIdToPacketHandler.TryGetValue(packetId, out var packetHandler))
                    {
                        Log.Error($"Could not handle packet (ID = {packetId}) as it does not have a registered handler.");
                        packet.Return();
                        continue;
                    }

                    packetHandler(packet.Reader, senderEndPoint);
                    packet.Return();
                }
            }

            lock (_mainThreadActions)
            {
                while (_mainThreadActions.Count > 0)
                {
                    _mainThreadActions.Dequeue()();
                }
            }
        }

        /// <summary>
        /// Registers a new packet handler for a packet with specific ID.
        /// </summary>
        public void RegisterPacketHandler(ushort packetId, Action<PacketReader, EndPoint> packetHandler) =>
            _packetIdToPacketHandler.Add(packetId, packetHandler);

        /// <summary>
        /// Returns true if packet handler for specific ID exists, false otherwise.
        /// </summary>
        public bool TryGetPacketHandler(ushort packedId, out Action<PacketReader, EndPoint> packetHandler) =>
            _packetIdToPacketHandler.TryGetValue(packedId, out packetHandler);

        /// <summary>
        /// Enqueues an action that will be executed on the main thread.
        /// </summary>
        /// <param name="action">Action to be executed on the main thread.</param>
        protected void ExecuteOnMainThread(Action action)
        {
            lock (_mainThreadActions)
            {
                _mainThreadActions.Enqueue(action);
            }
        }
    }
}