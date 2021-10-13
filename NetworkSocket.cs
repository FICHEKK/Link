using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Networking.Transport
{
    public class NetworkSocket : MonoBehaviour
    {
        private static readonly EndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);

        private readonly byte[] _receiveBuffer = new byte[1024];
        private Socket _socket;
        private IPacketHandler _packetHandler;

        public void Listen(Socket socket, IPacketHandler packetHandler)
        {
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

        protected virtual void HandlePacket(Packet packet, EndPoint senderEndPoint) =>
            _packetHandler.Handle(packet, senderEndPoint);

        public void Send(Packet packet, EndPoint receiverEndPoint) =>
            packet.Send(_socket, receiverEndPoint);

        private void OnDisable() =>
            _socket.Dispose();
    }
}
