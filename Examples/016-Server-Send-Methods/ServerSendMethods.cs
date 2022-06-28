using Link.Nodes;

namespace Link.Examples._016_Server_Send_Methods;

/// <summary>
/// This example explains and demonstrates how to use server's send methods.
/// There are 3 methods a server can use to send data to a client. Those are:
/// <list type="number">
/// <item><see cref="Server.SendToOne"/> which sends data to one specific client.</item>
/// <item><see cref="Server.SendToMany"/> which sends data to set of specific clients.</item>
/// <item><see cref="Server.SendToAll"/> which sends data to all of the connected clients.</item>
/// </list>
/// </summary>
public static class ServerSendMethods
{
    private const string IpAddress = "127.0.0.1";
    private const ushort Port = 7777;

    public static void Main()
    {
        using var server = new Server();
        server.ClientConnected += OnClientConnected;
        server.Start(Port);
        
        using var client1 = new Client();
        using var client2 = new Client();
        using var client3 = new Client();
        
        client1.AddHandler(args => Console.WriteLine($"Client 1 (port {args.Client.LocalEndPoint!.Port}) received: {args.Packet.Read<string>()}"));
        client2.AddHandler(args => Console.WriteLine($"Client 2 (port {args.Client.LocalEndPoint!.Port}) received: {args.Packet.Read<string>()}"));
        client3.AddHandler(args => Console.WriteLine($"Client 3 (port {args.Client.LocalEndPoint!.Port}) received: {args.Packet.Read<string>()}"));
        
        client1.Connect(IpAddress, Port);
        client2.Connect(IpAddress, Port);
        client3.Connect(IpAddress, Port);
        
        Console.ReadKey();
    }

    private static void OnClientConnected(Server.ClientConnectedEventArgs args)
    {
        // Extract server into a local variable for easy reuse.
        var server = args.Server;
        
        // Wait once all 3 clients are connected.
        if (server.ConnectionCount < 3) return;
        
        // Send a message to last connected client.
        server.SendToOne
        (
            Packet.Get(Delivery.Reliable).Write($"[{nameof(Server.SendToOne)}] Message to the last client."),
            clientEndPoint: args.Connection.RemoteEndPoint
        );

        // Send a message to first two clients.
        server.SendToMany
        (
            Packet.Get(Delivery.Reliable).Write($"[{nameof(Server.SendToMany)}] Message to the first two clients."),
            clientEndPoints: server.EndPoints.Where(ep => !ep.Equals(args.Connection.RemoteEndPoint))
        );
        
        // Send a message to all of the connected clients.
        server.SendToAll(Packet.Get(Delivery.Reliable).Write($"[{nameof(Server.SendToAll)}] Message to all clients!"));
    }
}
