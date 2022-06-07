using Link.Nodes;

namespace Link.Examples._014_Network_Events;

/// <summary>
/// This example lists and briefly explains all of the available network events that you can subscribe to.
/// <br/><br/>
/// Network events provide important information about the state changes in your networked application and
/// are essential for creating responsive user experience.
/// </summary>
public static class NetworkEvents
{
    public static void Main()
    {
        ListServerEvents();
        ListClientEvents();
    }

    private static void ListServerEvents()
    {
        var server = new Server();

        // Invoked each time server starts and begins listening for client connections.
        server.Started += (s, _) => Console.WriteLine($"Server started on port {s.Port}.");

        // Invoked each time a new client connects to the server.
        server.ClientConnected += (_, args) => Console.WriteLine($"Client from {args.Connection.RemoteEndPoint} has connected.");
        
        // Invoked each time an already connected client disconnects from the server.
        server.ClientDisconnected += (_, args) => Console.WriteLine($"Client from {args.Connection.RemoteEndPoint} has disconnected (cause: {args.Cause}).");

        // Invoked each time server stops and no longer listens for client connections.
        server.Stopped += (_, _) => Console.WriteLine("Server stopped.");
    }

    private static void ListClientEvents()
    {
        var client = new Client();

        // Invoked each time client starts the process of establishing connection with the server.
        client.Connecting += (_, _) => Console.WriteLine("Client is connecting to the server.");
        
        // Invoked each time client successfully connects to the server.
        client.Connected += (_, _) => Console.WriteLine("Client has connected to the server.");
        
        // Invoked each time client fails to establish a connection with the server.
        client.ConnectFailed += (_, _) => Console.WriteLine("Client failed to connect to the server.");
        
        // Invoked each time client disconnects from the server.
        client.Disconnected += (_, args) => Console.WriteLine($"Client has disconnected from the server (cause: {args.Cause}).");
    }
}
