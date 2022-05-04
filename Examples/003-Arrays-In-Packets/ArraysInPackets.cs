using Link.Nodes;

namespace Link.Examples._003_Arrays_In_Packets;

/// <summary>
/// This example explains and demonstrates all of the ways an array can be written to and read from the packet.
/// </summary>
public static class ArraysInPackets
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
        // Array which will be used to demonstrate writes to the packet.
        var array = new[] { 1, 2, 3, 4, 5 };

        // Packet to write arrays to.
        var packet = Packet.Get(Delivery.Reliable);
        
        // Basic usage - writes entire array, prefixed by array length.
        // This is the standard usage which will be used most of the time.
        // Array length is needed for the receiver of the packet to know
        // how many bytes of data "belongs" to the array.
        packet.WriteArray(array);

        // Writes entire array, but does not prefix it with array length.
        // This should be used only when the receiver knows beforehand
        // exactly how many elements to expect in the packet.
        packet.WriteArray(array, writeLength: false);

        // Writes a portion of the array, prefixed by portion length.
        // This example will write exactly 3 elements, starting from index 0.
        packet.WriteArray(array, start: 0, length: 3);
        
        // Writes a portion of the array, but does not prefix it with length.
        // This example will write exactly 3 elements, starting from index 0.
        packet.WriteArray(array, start: 0, length: 3, writeLength: false);

        // All the data was written and packet is ready for shipping.
        return packet;
    }

    private static void HandlePacket(ReadOnlyPacket packet)
    {
        // Basic array read - first reads number of elements from the packet,
        // then reads that many elements and returns the array.
        var basicArray = packet.ReadArray<int>();
        Console.WriteLine(string.Join(", ", basicArray));

        // Reads exactly 5 elements from the packet (since that many were
        // written in the packet).
        var fixedArray = packet.ReadArray<int>(length: 5);
        Console.WriteLine(string.Join(", ", fixedArray));

        // Portion of the array is read exactly as you would a normal array.
        var portionArray = packet.ReadArray<int>();
        Console.WriteLine(string.Join(", ", portionArray));

        // We wrote a portion of exactly 3 elements and no length prefix,
        // so we explicitly read that many elements.
        var fixedPortionArray = packet.ReadArray<int>(length: 3);
        Console.WriteLine(string.Join(", ", fixedPortionArray));
    }
}
