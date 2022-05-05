using Link.Channels;
using Link.Nodes;

namespace Link.Examples._007_Custom_Channels;

/// <summary>
/// This example demonstrates how to define and use your own custom channels.
/// Custom channels are great for keeping track of bandwidth usage and making
/// your application cleaner and easier to understand.
/// <br/><br/>
/// For example, you can easily define your custom channel on which only text
/// messages will be sent and received in your application. By reading bandwidth
/// usage of that channel, you can get information about how much bandwidth is
/// used only on text messages.
/// <br/><br/>
/// On the other hand, you can also easily find all of the places in your
/// application where text message is being sent by simply searching for
/// usages of your channel identifier.
/// </summary>
public static class CustomChannels
{
    private const string IpAddress = "127.0.0.1";
    private const int Port = 7777;

    // Identifier of our custom channel. Channel identifiers from 0 to 15 are reserved by the library.
    private const byte TextMessageChannelId = 16;

    public static void Main()
    {
        using var server = new Server();
        server.PacketReceived += (packet, _) => Console.WriteLine($"Packet received on channel {packet.ChannelId}.");
        server.ConnectionInitializer = AddCustomChannel;
        server.Start(Port);

        using var client = new Client();
        client.Connected += () => SendPacketOnCustomChannel(client);
        client.ConnectionInitializer = AddCustomChannel;
        client.Connect(IpAddress, Port);

        Console.ReadKey();
    }

    private static void AddCustomChannel(Connection connection)
    {
        // Each time a new connection is created, initialize it with our custom channel.
        connection[TextMessageChannelId] = new ReliablePacketChannel(connection, isOrdered: true)
        {
            Name = "Text channel",
            MaxResendAttempts = 20,
            MinResendDelay = 10,
            BackoffFactor = 1.5,
        };
    }
    
    private static void SendPacketOnCustomChannel(Client client)
    {
        // This is another overload which takes in channel ID.
        var packet = Packet.Get(channelId: TextMessageChannelId);
        
        // Send packet on our custom channel.
        client.Send(packet);
    }
}