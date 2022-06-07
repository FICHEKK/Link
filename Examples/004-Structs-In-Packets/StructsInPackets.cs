using Link.Nodes;

namespace Link.Examples._004_Structs_In_Packets;

/// <summary>
/// This example demonstrates how to read and write custom structs.
/// </summary>
public static class StructsInPackets
{
    private const string IpAddress = "127.0.0.1";
    private const ushort Port = 7777;

    public static void Main()
    {
        using var server = new Server();
        server.AddHandler((_, packet, _) => HandlePacket(packet));
        server.Start(Port);

        using var client = new Client();
        client.Connected += (c, _) => c.Send(CreatePacket());
        client.Connect(IpAddress, Port);

        Console.ReadKey();
    }

    private static Packet CreatePacket()
    {
        // Packet to write structs to.
        var packet = Packet.Get(Delivery.Reliable);
        
        // We can easily write any custom struct to the packet just
        // as we would any other primitive type. The only constraint
        // is that struct must be unmanaged type.
        packet.Write(new Point(1, 2, 3));

        // We can even write nested structs just as easily.
        var line = new Line(start: new Point(4, 5, 6), end: new Point(7, 8, 9));
        packet.Write(line);

        // Even arrays of structs work!
        packet.WriteArray(new Point[] { new(0, 0, 0), new(1, 1, 1), new(2, 2, 2) });
        packet.WriteArray(new[] { line, line, line });
        
        // Packet is done, ship it!
        return packet;
    }

    private static void HandlePacket(ReadOnlyPacket packet)
    {
        // Read custom structs the same way you should any other primitive.
        var point = packet.Read<Point>();
        var line = packet.Read<Line>();
        var points = packet.ReadArray<Point>();
        var lines = packet.ReadArray<Line>();
        
        Console.WriteLine($"Point: {point}");
        Console.WriteLine($"Line: {line}");
        Console.WriteLine($"Points: {string.Join(", ", points)}");
        Console.WriteLine($"Lines: {string.Join(", ", lines)}");
    }

    // Our custom data structure that we wish to read/write to the packet.
    private readonly struct Point
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        
        public Point(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    // We can even have nested structures.
    private readonly struct Line
    {
        public Point Start { get; }
        public Point End { get; }

        public Line(Point start, Point end)
        {
            Start = start;
            End = end;
        }

        public override string ToString() => $"{Start} - {End}";
    }
}
