using Link.Nodes;

namespace Link.Examples._003_Arrays_In_Packets;

/// <summary>
/// This example explains and demonstrates all of the ways an array can be written to and read from the packet.
/// </summary>
public static class ArraysInPackets
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
        // Array which will be used to demonstrate writes to the packet.
        var array = new[] { 1, 2, 3, 4, 5 };

        // Packet to write arrays to.
        var packet = Packet.Get(Delivery.Reliable);
        
        // Basic usage - writes entire array, prefixed by array length.
        // This is the standard usage which will be used most of the time.
        // Array length is needed for the receiver of the packet to know
        // how many bytes of data "belongs" to the array.
        packet.Write(array);

        // Writes a segment of the array, prefixed by portion length.
        // This example will write exactly 3 elements, starting from index 1.
        packet.Write(new ArraySegment<int>(array, offset: 1, count: 3));

        // You can also easily write jagged arrays of any dimension!
        packet.Write(new[]
        {
            new[] { 1 },
            new[] { 2, 3 },
            new[] { 4, 5, 6 }
        });

        // All the data was written and packet is ready for shipping.
        return packet;
    }

    private static void HandlePacket(ReadOnlyPacket packet)
    {
        // Basic array read - first reads number of elements from the packet,
        // then reads that many elements and returns the array.
        var basicArray = packet.Read<int[]>();
        Console.WriteLine("Basic array:");
        Console.WriteLine(string.Join(", ", basicArray) + Environment.NewLine);

        // Segment of an array is read exactly as you would a normal array.
        var arraySegment = packet.Read<int[]>();
        Console.WriteLine("Array segment:");
        Console.WriteLine(string.Join(", ", arraySegment) + Environment.NewLine);

        // Reading jagged arrays is just as simple!
        var jaggedArray = packet.Read<int[][]>();
        Console.WriteLine("Jagged array:");
        foreach (var arr in jaggedArray) Console.WriteLine(string.Join(", ", arr));
    }
}
