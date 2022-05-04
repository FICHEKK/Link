using Link.Nodes;

namespace Link.Examples._002_Complex_Packet;

/// <summary>
/// This example explains and demonstrates how more complex packets can be created and handled.
/// </summary>
public static class ComplexPacket
{
    private const string IpAddress = "127.0.0.1";
    private const int Port = 7777;

    public static void Main()
    {
        using var server = new Server();
        server.PacketReceived += (packet, _) => HandlePacket(packet);
        server.Start(Port);

        using var client = new Client();
        client.Connected += () => client.Send(CreatePacket());
        client.Connect(IpAddress, Port);

        Console.ReadKey();
    }

    /// <summary>
    /// Packets can only be created using static construction methods of <see cref="Packet"/> struct.
    /// </summary>
    /// <remarks>
    /// Since packet is a struct, it can be created using the default constructor (which cannot be
    /// disabled at the time of writing this). However, any attempts at doing anything with that
    /// packet will result in an exception being thrown.
    /// </remarks>
    private static Packet CreatePacket()
    {
        // Creates empty packet that will be sent on the reliable channel.
        var packet = Packet.Get(Delivery.Reliable);

        // Writes a string to the packet.
        packet.Write("Text");

        // Writes an integer to the packet.
        packet.Write(12345);

        // Writes an array of double values to the packet.
        packet.WriteArray(new[] { 4.0, 5.0, 6.0 });

        // Write methods can also be chained.
        packet.Write('A').Write('B').Write('C');

        // We are done writing to the packet.
        return packet;
    }

    /// <summary>
    /// When handling the received packet, you get an instance of <see cref="ReadOnlyPacket"/>,
    /// which is a safety measure to make sure data in the packet is not modified by accident.
    /// </summary>
    private static void HandlePacket(ReadOnlyPacket packet)
    {
        // When we created the packet, first thing we wrote was a string. Now that we are 
        // reading the packet, we need to respect the order in which data was written,
        // so the first thing we must read is a string.
        Console.WriteLine(packet.ReadString());

        // Next thing on the list is an integer.
        Console.WriteLine(packet.Read<int>());

        // Then an array of doubles...
        var array = packet.ReadArray<double>();
        Console.WriteLine(string.Join(", ", array));

        // Finally, the 3 characters.
        Console.WriteLine(packet.Read<char>());
        Console.WriteLine(packet.Read<char>());
        Console.WriteLine(packet.Read<char>());
    }
}
