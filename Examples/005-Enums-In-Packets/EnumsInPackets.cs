using Link.Nodes;

namespace Link.Examples._005_Enums_In_Packets;

/// <summary>
/// This example demonstrates how to read and write custom enums.
/// The process is extremely simple - define your enum and read/write
/// it just as you would any other primitive value.
/// </summary>
public static class EnumsInPackets
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

    private static Packet CreatePacket()
    {
        // Packet to write structs to.
        var packet = Packet.Get(Delivery.Reliable);
        
        // Write enum values just as any other primitive.
        packet.Write(Direction.North);
        packet.Write(Direction.East);
        packet.Write(Direction.South);
        packet.Write(Direction.West);

        // Even write arrays of enums!
        packet.WriteArray(new[] { Direction.East, Direction.West });

        // Packet is done, ship it!
        return packet;
    }

    private static void HandlePacket(ReadOnlyPacket packet)
    {
        // Read custom enum the same way you should any other primitive.
        Console.WriteLine(packet.Read<Direction>());
        Console.WriteLine(packet.Read<Direction>());
        Console.WriteLine(packet.Read<Direction>());
        Console.WriteLine(packet.Read<Direction>());

        var directions = packet.ReadArray<Direction>();
        Console.WriteLine($"Directions: {string.Join(", ", directions)}");
    }

    // Our custom enum that we wish to read/write to the packet.
    // Tip: By default, underlying enum type is int (which takes 4 bytes). If
    // possible, make your underlying enum type byte to preserve bandwidth.
    private enum Direction : byte
    {
        North,
        East,
        South,
        West,
    }
}
