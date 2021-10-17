using System;
using System.Net;
using System.Net.Sockets;

namespace Networking.Transport
{
    public static class SocketExtensions
    {
        public static void BeginSendTo(this Socket socket, byte[] buffer, int length, EndPoint receiverEndPoint) => socket.BeginSendTo(
            buffer: buffer,
            offset: 0,
            size: length,
            socketFlags: SocketFlags.None,
            remoteEP: receiverEndPoint,
            callback: null,
            state: null
        );

        public static void BeginReceiveFrom(this Socket socket, byte[] receiveBuffer, ref EndPoint remoteEndPoint, AsyncCallback callback) => socket.BeginReceiveFrom(
            buffer: receiveBuffer,
            offset: 0,
            size: receiveBuffer.Length,
            socketFlags: SocketFlags.None,
            remoteEP: ref remoteEndPoint,
            callback: callback,
            state: null
        );
    }
}
