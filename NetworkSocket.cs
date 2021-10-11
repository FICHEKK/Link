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
        private Action<Packet, EndPoint> _handleReceivedPacket;

        public void Initialize(Socket socket, Action<Packet, EndPoint> handleReceivedPacket)
        {
            _socket = socket ?? throw new NullReferenceException(nameof(socket));
            _handleReceivedPacket = handleReceivedPacket ?? throw new NullReferenceException(nameof(handleReceivedPacket));
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
                Debug.Log(e.Message.Length);
                Debug.LogWarning($"Socket exception {e.ErrorCode}, {e.SocketErrorCode}: {e.Message}");
                ReceiveFromAnySource();
            }
        }

        protected virtual void HandlePacket(Packet packet, EndPoint senderEndPoint) =>
            _handleReceivedPacket(packet, senderEndPoint);

        public void Send(Packet packet, EndPoint receiverEndPoint) =>
            packet.Send(_socket, receiverEndPoint);

        private void OnDestroy() =>
            _socket.Dispose();
    }
}
