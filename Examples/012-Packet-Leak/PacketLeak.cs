namespace Link.Examples._012_Packet_Leak;

/// <summary>
/// This example demonstrates how a packet leak can occur and how to ensure that it never happens.
/// Packet leak occurs when a packet gets created, but not used. But why is this a problem?
/// <br/><br/>
/// Each time a new packet is created, it internally borrows a reusable buffer (byte-array). When
/// a packet is sent using any of the client or server send methods, that method it will perform
/// returning of the borrowed buffer for you.
///<br/><br/>
/// However, if you do not send the packet, buffer will not get returned properly. In that case,
/// packet must be returned manually, using the <see cref="Packet.Return"/> method.
/// <br/><br/>
/// In short, each time you create a packet, you must either-or:
/// <list type="bullet">
///     <item>Send the packet.</item>
///     <item>Return the packet.</item>
/// </list>
/// In order to inform you of packet leaks, each time internal buffer gets destructed improperly,
/// it is going to log a warning.
/// </summary>
public static class PacketLeak
{
    public static void Main()
    {
        // Initialize logger so we can see the warning.
        Log.Warning = Console.WriteLine;

        // Leak 3 packets.
        CausePacketLeak();
        CausePacketLeak();
        CausePacketLeak();
        
        // Properly use a packet.
        PreventPacketLeak();

        // Force garbage collector to run.
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <summary>
    /// This method will cause a leak.
    /// </summary>
    private static void CausePacketLeak()
    {
        // Packet was created, but not used, creating a leak.
        Packet.Get(Delivery.Reliable);
    }

    /// <summary>
    /// This method will NOT cause a leak.
    /// </summary>
    private static void PreventPacketLeak()
    {
        var packet = Packet.Get(Delivery.Reliable);
        
        // Packet was created, but not sent, so we return it.
        packet.Return();
    }
}
