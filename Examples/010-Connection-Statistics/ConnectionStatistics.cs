using System.Net;
using Link.Nodes;

namespace Link.Examples._010_Connection_Statistics;

/// <summary>
/// This example demonstrates how to easily get information about connection statistics.
/// Term "statistics" includes information about the state of the connection such as
/// round-trip-time or number of packets/bytes sent/received.
/// </summary>
public static class ConnectionStatistics
{
    private const string IpAddress = "127.0.0.1";
    private const ushort Port = 7777;

    public static void Main()
    {
        using var server = new Server();
        server.AddHandler(Echo);
        server.Start(Port);

        using var client = new Client();
        client.Connected += SendMessages;
        client.Connect(IpAddress, Port);

        Console.ReadKey();
    }

    /// <summary>
    /// Simple method that reads client's message and sends the same message back on the
    /// same channel on which it was received.
    /// </summary>
    private static void Echo(Server server, ReadOnlyPacket packet, EndPoint clientEndPoint)
    {
        // Read the message client sent us.
        var message = packet.ReadString();

        // Echo it back to the client.
        server.SendToOne(Packet.Get(packet.ChannelId).Write(message), clientEndPoint);
    }

    private static async void SendMessages(Client.ConnectedEventArgs args)
    {
        await SendMessageAndPrintStatistics(args.Client, Delivery.Unreliable, "Hello server!");
        await SendMessageAndPrintStatistics(args.Client, Delivery.Sequenced, "Sending data...");
        await SendMessageAndPrintStatistics(args.Client, Delivery.Reliable, "Final message!");
    }

    /// <summary>
    /// Sends given message, waits for a second and then prints connection statistics.
    /// </summary>
    private static async Task SendMessageAndPrintStatistics(Client client, Delivery delivery, string message)
    {
        client.Send(Packet.Get(delivery).Write(message));

        await Task.Delay(millisecondsDelay: 1000);
        PrintConnectionStatistics(client.Connection!);
    }

    private static void PrintConnectionStatistics(Connection connection)
    {
        // It is that easy to get information about a specific connection.
        Console.WriteLine("Connection statistics:");
        Console.WriteLine($"Sent: {connection.PacketsSent} packets, {connection.BytesSent} bytes");
        Console.WriteLine($"Received: {connection.PacketsReceived} packets, {connection.BytesReceived} bytes");
        Console.WriteLine($"Round-trip time: {connection.RoundTripTime} ms");
        Console.WriteLine();

        // We can also get information of any channel that connection has.
        var unreliableChannel = connection[(byte) Delivery.Unreliable]!;
        var sequencedChannel = connection[(byte) Delivery.Sequenced]!;
        var reliableChannel = connection[(byte) Delivery.Reliable]!;

        Console.WriteLine($"Connection has {connection.ChannelCount} channels:");
        Console.WriteLine($"{nameof(Delivery.Unreliable)} channel sent {unreliableChannel.PacketsSent} packets.");
        Console.WriteLine($"{nameof(Delivery.Sequenced)} channel sent {sequencedChannel.PacketsSent} packets.");
        Console.WriteLine($"{nameof(Delivery.Reliable)} channel sent {reliableChannel.PacketsSent} packets.");
        
        Console.WriteLine("========================================");
    }
}
