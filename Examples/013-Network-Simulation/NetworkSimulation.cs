using Link.Nodes;

namespace Link.Examples._013_Network_Simulation;

/// <summary>
/// This example explains and demonstrates the concept of network simulation.
/// <br/><br/>
/// When building a networking application using local network, connection is
/// almost always going to be flawless. There will be no packet loss, latency
/// will be minuscule and there will generally be no problems.
/// <br/><br/>
/// However, real-life networks are unpredictable; packet-loss will occur and
/// latency is going to vary. In order to test how your application would behave
/// under those real network conditions, you can use network simulation.
/// <br/><br/>
/// Network simulation allows you to set artificial packet-loss and latency.
/// </summary>
public static class NetworkSimulation
{
    private const string IpAddress = "127.0.0.1";
    private const ushort Port = 7777;

    public static void Main()
    {
        using var server = new Server();
        server.AddHandler((_, packet, _) => HandlePacket(packet));
        server.Start(Port);

        // Each packet is going to be held randomly for 200 to 500 ms to simulate latency. 
        server.MinLatency = 200;
        server.MaxLatency = 500;

        // Server is going to randomly drop about 30% of packets.
        server.PacketLoss = 0.3;

        using var client = new Client();
        client.Connected += (c, _) => SendPacketsForEachDelivery(c, count: 10);
        client.Connect(IpAddress, Port);

        Console.ReadKey();
    }

    private static void SendPacketsForEachDelivery(Client client, int count)
    {
        var deliveries = (Delivery[]) Enum.GetValues(typeof(Delivery));

        // Send packets one-by-one on each channel.
        for (var i = 0; i < count; i++)
        {
            foreach (var delivery in deliveries)
            {
                var message = $"Packet {i} on '{delivery}' channel.";
                client.Send(Packet.Get(delivery).Write(message));
            }
        }
    }

    private static void HandlePacket(ReadOnlyPacket packet)
    {
        var indentation = new string('\t', count: packet.ChannelId - (byte) Delivery.Unreliable);
        var message = packet.ReadString();
        Console.WriteLine(indentation + message);
    }
}
