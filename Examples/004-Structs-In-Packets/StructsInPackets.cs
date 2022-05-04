using Link.Nodes;

namespace Link.Examples._004_Structs_In_Packets;

/// <summary>
/// This example demonstrates how to read and write custom structs.
/// </summary>
public static class StructsInPackets
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
        // We can easily write any custom struct to the packet just
        // as we would any other primitive type. The only constraint
        // is that struct must be unmanaged type.
        return Packet.Get(Delivery.Reliable).Write(new Point(1, 2, 3));
    }

    private static void HandlePacket(ReadOnlyPacket packet)
    {
        // Read custom struct as if it was any other primitive.
        var point = packet.Read<Point>();
        Console.WriteLine($"Point: ({point.X}, {point.Y}, {point.Z})");
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
    }
}
