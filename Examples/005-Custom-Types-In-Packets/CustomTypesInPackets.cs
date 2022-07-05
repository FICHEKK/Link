using Link.Nodes;
using Link.Serialization;

namespace Link.Examples._005_Custom_Types_In_Packets;

/// <summary>
/// This example demonstrates how to read and write custom types.
/// Very often you need to write your own custom data-types to a
/// packet. Since this is a type under your control, you need to
/// define how that type should be written and read to and from a
/// packet.
/// <br/><br/>
/// Fortunately, the process is extremely simple: define, implement
/// and register read and write methods of your custom data-type.
/// </summary>
public static class CustomTypesInPackets
{
    private const string IpAddress = "127.0.0.1";
    private const ushort Port = 7777;

    public static void Main()
    {
        using var server = new Server();
        server.AddHandler(args => HandlePacket(args.Packet));
        server.Start(Port);

        using var client = new Client();
        client.Connected += args => args.Client.Send(CreatePacket());
        client.Connect(IpAddress, Port);

        Console.ReadKey();
    }

    private static Packet CreatePacket()
    {
        // Packet to write structs to.
        var packet = Packet.Get(Delivery.Reliable);
        
        // We can easily write any registered custom data-type just
        // as we would any other primitive type.
        packet.Write(new Point(1, 2, 3));

        // We can even write nested data structures just as easily.
        var line = new Line { StartPoint = new Point(4, 5, 6), EndPoint = new Point(7, 8, 9) };
        packet.Write(line);

        // Even arrays will automatically work!
        packet.Write(new Point[] { new(0, 0, 0), new(1, 1, 1), new(2, 2, 2) });
        packet.Write(new[] { line, line, line });
        
        // Packet is done, ship it!
        return packet;
    }

    private static void HandlePacket(ReadOnlyPacket packet)
    {
        // Reading custom data-types is as easy as reading primitive types.
        var point = packet.Read<Point>();
        var line = packet.Read<Line>();
        var points = packet.Read<Point[]>();
        var lines = packet.Read<Line[]>();
        
        Console.WriteLine($"Point: {point}");
        Console.WriteLine($"Line: {line}");
        Console.WriteLine($"Points: {string.Join(", ", points)}");
        Console.WriteLine($"Lines: {string.Join<Line>(", ", lines)}");
    }

    private readonly struct Point : ISerializer<Point>
    {
        public float X { get; init; }
        public float Y { get; init; }
        public float Z { get; init; }
        
        public Point(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public void Write(Packet packet, Point point)
        {
            packet.Write(point.X);
            packet.Write(point.Y);
            packet.Write(point.Z);
        }

        public Point Read(ReadOnlyPacket packet) => new()
        {
            X = packet.Read<float>(),
            Y = packet.Read<float>(),
            Z = packet.Read<float>(),
        };

        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    private class Line : ISerializer<Line>
    {
        public Point StartPoint { get; init; }
        public Point EndPoint { get; init; }

        public void Write(Packet packet, Line line)
        {
            // Serialization system also knows how to serialize 
            // points since we also defined that behavior.
            packet.Write(line.StartPoint);
            packet.Write(line.EndPoint);
        }

        public Line Read(ReadOnlyPacket packet) => new()
        {
            StartPoint = packet.Read<Point>(),
            EndPoint = packet.Read<Point>(),
        };

        public override string ToString() => $"{StartPoint} - {EndPoint}";
    }
}
