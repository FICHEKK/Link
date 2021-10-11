using System;
using System.Net;
using System.Net.Sockets;

namespace Networking.Transport
{
    public static class SocketExtensions
    {
        public static IAsyncResult BeginReceiveFrom(this Socket socket, byte[] receiveBuffer, ref EndPoint remoteEndPoint, AsyncCallback callback) => socket.BeginReceiveFrom(
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
