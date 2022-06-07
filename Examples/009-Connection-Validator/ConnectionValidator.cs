using System.Net;
using Link.Nodes;

namespace Link.Examples._009_Connection_Validator;

/// <summary>
/// This example explains and demonstrates the concept of a connection validator.
/// Connection validator is a server-side only component that acts as a filter for
/// determining if a connection from client should be accepted or not. By default,
/// every new client connection is accepted, but this behaviour is often times not
/// desirable.
/// <br/><br/>
/// For example, if you wish to limit the number of client connections your server
/// can have, you can do that in the validator. Or if you wish to add a protective
/// feature, such as a password that client needs to supply in order to connect,
/// validator is the way to go.
/// <br/><br/>
/// In order to make validation process more powerful, client also has the ability
/// to send additional data in the connect packet. All of this can be seen in the
/// example below. 
/// </summary>
public static class ConnectionValidator
{
    private const string IpAddress = "127.0.0.1";
    private const ushort Port = 7777;
    private const string RequiredServerPassword = "ServerPassword123";

    public static void Main()
    {
        using var server = new Server();
        server.ConnectionValidator = ValidateClientConnection;
        server.Start(Port);

        using var client1 = new Client();
        using var client2 = new Client();
        using var client3 = new Client();
        
        client1.Connected += (_, _) => Console.WriteLine("Client 1 successfully connected!");
        client1.ConnectFailed += (_, _) => Console.WriteLine("Client 1 failed to connect...");
        
        client2.Connected += (_, _) => Console.WriteLine("Client 2 successfully connected!");
        client2.ConnectFailed += (_, _) => Console.WriteLine("Client 2 failed to connect...");
        
        client3.Connected += (_, _) => Console.WriteLine("Client 3 successfully connected!");
        client3.ConnectFailed += (_, _) => Console.WriteLine("Client 3 failed to connect...");

        // We can very easily write data in the connect packet by supplying implementation of connect
        // packet factory. This example's implementation simply writes password, which the server will
        // read and use for determining if the connection should be accepted.
        client1.Connect(IpAddress, Port, maxAttempts: 1, connectPacketFactory: packet => packet.Write(RequiredServerPassword));
        
        // Another client that sends an invalid password.
        client2.Connect(IpAddress, Port, maxAttempts: 1, connectPacketFactory: packet => packet.Write("InvalidPassword"));
        
        // And a client that doesn't send password at all.
        client3.Connect(IpAddress, Port, maxAttempts: 1);

        Console.ReadKey();
    }

    /// <summary>
    /// This is our custom connection validation logic. Connection is accepted only
    /// if client provides the correct password, otherwise it is declined.
    /// </summary>
    private static bool ValidateClientConnection(ReadOnlyPacket connectPacket, EndPoint clientEndPoint)
    {
        try
        {
            var passwordSentByClient = connectPacket.ReadString();
            var isAccepted = passwordSentByClient == RequiredServerPassword;

            Console.WriteLine($"Client from {clientEndPoint} has been {(isAccepted ? "accepted!" : "declined.")}");
            return isAccepted;
        }
        catch
        {
            Console.WriteLine($"Client from {clientEndPoint} did not even supply a password.");
            return false;
        }
    }
}
