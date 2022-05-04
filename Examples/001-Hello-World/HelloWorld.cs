using Link.Nodes;

namespace Link.Examples._001_Hello_World;

/// <summary>
/// This is the "Hello world!" of <see cref="Link"/> library.
/// <br/><br/>
/// In this basic example, in less than 10 lines of code, <see cref="Link"/> manages to:
/// <list type="number">
///     <item>Start server on specified port.</item>
///     <item>Create client and connect to the server.</item>
///     <item>Construct reliable packet and send it from client to server when client connects.</item>
///     <item>Listen and react to received packet on the server.</item>
///     <item>Clean-up all the used network resources.</item>
/// </list>
/// And this is just the beginning! <see cref="Link"/> offers many more amazing features, which are
/// just as simple and easy-to-use.
/// </summary>
public static class HelloWorld
{
    private const string IpAddress = "127.0.0.1";
    private const int Port = 7777;
    private const string Message = "Hello world!";
    
    public static void Main()
    {
        using var server = new Server();
        server.PacketReceived += (packet, _) => Console.WriteLine(packet.ReadString());
        server.Start(Port);

        using var client = new Client();
        client.Connected += () => client.Send(Packet.Get(Delivery.Reliable).Write(Message));
        client.Connect(IpAddress, Port);

        Console.ReadKey();
    }
}
