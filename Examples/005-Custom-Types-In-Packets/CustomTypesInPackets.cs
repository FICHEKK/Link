using Link.Nodes;

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
    
    /// <summary>
    /// Defines how an instance of <see cref="Point"/> should be written to a packet.
    /// </summary>
    private static void WritePoint(Packet packet, Point point)
    {
        packet.Write(point.X);
        packet.Write(point.Y);
        packet.Write(point.Z);
    }

    /// <summary>
    /// Defines how an instance of <see cref="Point"/> should be read from a packet.
    /// </summary>
    private static Point ReadPoint(ReadOnlyPacket packet)
    {
        var x = packet.Read<float>();
        var y = packet.Read<float>();
        var z = packet.Read<float>();
        return new Point(x, y, z);
    }
    
    /// <summary>
    /// Defines how an instance of <see cref="Line"/> should be written to a packet.
    /// </summary>
    private static void WriteLine(Packet packet, Line line)
    {
        // Serialization system also knows how to serialize 
        // points since we also defined that behavior.
        packet.Write(line.StartPoint);
        packet.Write(line.EndPoint);
    }

    /// <summary>
    /// Defines how an instance of <see cref="Line"/> should be read from a packet.
    /// </summary>
    private static Line ReadLine(ReadOnlyPacket packet)
    {
        var startPoint = packet.Read<Point>();
        var endPoint = packet.Read<Point>();
        return new Line(startPoint, endPoint);
    }

    private static Packet CreatePacket()
    {
        // Packet to write structs to.
        var packet = Packet.Get(Delivery.Reliable);
        
        // We can easily write any registered custom data-type just
        // as we would any other primitive type.
        packet.Write(new Point(1, 2, 3));

        // We can even write nested data structures just as easily.
        var line = new Line(startPoint: new Point(4, 5, 6), endPoint: new Point(7, 8, 9));
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

    private class Line
    {
        public Point StartPoint { get; }
        public Point EndPoint { get; }

        public Line(Point startPoint, Point endPoint)
        {
            StartPoint = startPoint;
            EndPoint = endPoint;
        }

        public override string ToString() => $"{StartPoint} - {EndPoint}";
    }
}
