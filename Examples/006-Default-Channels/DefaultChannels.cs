using Link.Nodes;

namespace Link.Examples._006_Default_Channels;

/// <summary>
/// This example explains and demonstrates how to send packets on different types of default channels.
/// Just as with previous examples, this process is extremely simple - just choose it.
/// </summary>
public static class DefaultChannels
{
    private const string IpAddress = "127.0.0.1";
    private const int Port = 7777;

    public static void Main()
    {
        using var server = new Server();
        server.PacketReceived += (packet, _) => Console.WriteLine($"Packet received on channel {packet.ChannelId}.");
        server.Start(Port);

        using var client = new Client();
        client.Connected += (c, _) => SendPacketOnEachDefaultChannel(c);
        client.Connect(IpAddress, Port);

        Console.ReadKey();
    }

    // So far, we have always created a packet that uses reliable
    // delivery. That means that this packet should be sent over
    // the reliable channel, meaning that library will ensure that
    // this packet is received, will be in order (relative to other
    // sent packets) and will not be duplicated on the receiver.
    //
    // Different channels offer different functionality and features.
    // This library by default has multiple built-in channel types,
    // and those built-in channels are accessed using delivery enum.
    private static void SendPacketOnEachDefaultChannel(Client client)
    {
        client.Send(Packet.Get(Delivery.Unreliable));
        client.Send(Packet.Get(Delivery.Sequenced));
        client.Send(Packet.Get(Delivery.Reliable));
    }
}
