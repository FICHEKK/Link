using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Networking.Transport
{
    /// <summary>
    /// Represents a network node - a fundamental building block
    /// of any network graph that can send and receive data.
    /// </summary>
    public abstract class Node
    {
        private static readonly EndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        /// <summary>
        /// Returns packet handlers registered for this network node.
        /// </summary>
        public IReadOnlyDictionary<ushort, Action<PacketReader, EndPoint>> PacketIdToPacketHandler => _packetIdToPacketHandler;

        /// <summary>
        /// Returns true if this node is currently listening for incoming packets.
        /// </summary>
        public bool IsListening => _socket != null;

        private readonly Dictionary<ushort, Action<PacketReader, EndPoint>> _packetIdToPacketHandler = new Dictionary<ushort, Action<PacketReader, EndPoint>>();
        private readonly Queue<(Packet packet, EndPoint senderEndPoint)> _pendingPackets = new Queue<(Packet, EndPoint)>();
        private readonly Queue<Action> _mainThreadActions = new Queue<Action>();
        private readonly byte[] _receiveBuffer = new byte[4096];
        private Socket _socket;

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
                if (bytesReceived == 0) goto ReceiveFromAnySource;

                var packet = Receive(_receiveBuffer, bytesReceived, senderEndPoint);
                if (packet == null) goto ReceiveFromAnySource;

                lock (_pendingPackets) _pendingPackets.Enqueue((packet, senderEndPoint));

                ReceiveFromAnySource:
                ReceiveFromAnySource();
            }
            catch (ObjectDisposedException)
            {
                Log.Info("Socket has been disposed.");
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // This is a known ICMP message received when destination port is unreachable.
                    // This occurs when remote end-point disconnects without sending the disconnect
                    // packet (or if disconnect packet did not arrive). This error does not need to
                    // be handled as the connection will get cleaned automatically once it times out.
                    ReceiveFromAnySource();
                    return;
                }

                Log.Warning($"Socket exception {e.ErrorCode}, {e.SocketErrorCode}: {e.Message}");
                ReceiveFromAnySource();
            }
            catch (Exception e)
            {
                Log.Error($"Unexpected exception: {e}");
                ReceiveFromAnySource();
            }
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
        public void Send(Packet packet, EndPoint receiverEndPoint)
        {
            SendWithoutReturningToPool(packet, receiverEndPoint);
            packet.Return();
        }

        protected void SendWithoutReturningToPool(Packet packet, EndPoint receiverEndPoint)
        {
            var buffer = packet.Buffer;
            var size = packet.Writer.WritePosition;
            _socket.SendTo(buffer, offset: 0, size, SocketFlags.None, receiverEndPoint);
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
