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
        server.Started += () => Console.WriteLine("Server started.");

        // Invoked each time a new client connects to the server.
        server.ClientConnected += connection => Console.WriteLine($"Client from {connection.RemoteEndPoint} has connected.");
        
        // Invoked each time a packet is received from a client.
        server.PacketReceived += (packet, clientEndPoint) => Console.WriteLine($"Client from {clientEndPoint} sent packet of size {packet.Size}.");
        
        // Invoked each time an already connected client disconnects from the server.
        server.ClientDisconnected += connection => Console.WriteLine($"Client from {connection.RemoteEndPoint} has disconnected.");

        // Invoked each time server stops and no longer listens for client connections.
        server.Stopped += () => Console.WriteLine("Server stopped.");
    }

    private static void ListClientEvents()
    {
        var client = new Client();

        // Invoked each time client starts the process of establishing connection with the server.
        client.Connecting += () => Console.WriteLine("Client is connecting to the server.");
        
        // Invoked each time client successfully connects to the server.
        client.Connected += () => Console.WriteLine("Client has connected to the server.");
        
        // Invoked each time a packet is received from the server.
        client.PacketReceived += (packet, serverEndPoint) => Console.WriteLine($"Server from {serverEndPoint} sent packet of size {packet.Size}.");
        
        // Invoked each time client fails to establish a connection with the server.
        client.ConnectFailed += () => Console.WriteLine("Client failed to connect to the server.");
        
        // Invoked each time client disconnects from the server.
        client.Disconnected += () => Console.WriteLine("Client has disconnected from the server.");
    }
}
