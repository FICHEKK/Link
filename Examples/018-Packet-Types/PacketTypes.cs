using System.Net;
using System.Numerics;
using Link.Nodes;

namespace Link.Examples._018_Packet_Types;

/// <summary>
/// So far, we have been sending only a single type of packet to keep things simple.
/// However, almost all networked applications are going to require multiple packet
/// types.
///<br/><br/>
/// For example, maybe we would like to define a text-message packet so we can easily
/// communicate with other players in the game. If a text-message packet is received,
/// we would like to print received message to the screen. But also we would like to
/// define a player-position packet which is going to carry information about where
/// in the world a specific player currently resides. If a player-position packet is
/// received, we would like to update the player's position in the world.
/// <br/><br/>
/// Based on this simple example, we can see that we need some way to differentiate
/// between different packet types. Not only that, but we also need a way to handle
/// different packets in their own specific ways. This example demonstrates just how
/// easy it is to create and handle any number of different types of packets. 
/// </summary>
public static class PacketTypes
{
    private const string IpAddress = "127.0.0.1";
    private const ushort Port = 7777;

    public static void Main()
    {
        using var server = new Server();
        
        // Add method that is going to handle text-message packets.
        server.AddHandler(HandleTextMessagePacket, PacketType.TextMessage.Id());
        
        // Add method that is going to handle player-position packets.
        server.AddHandler(HandlePlayerPositionPacket, PacketType.PlayerPosition.Id());
        
        // Once all handlers are added, we can start the server.
        server.Start(Port);
        
        using var client = new Client();
        client.Connected += SendPacketsOfAllTypes;
        client.Connect(IpAddress, Port);
        
        Console.ReadKey();
    }

    private static void SendPacketsOfAllTypes(Client client, Client.ConnectedEventArgs args)
    {
        // First we send a text-message packet.
        client.Send(PacketType.TextMessage.Get(Delivery.Reliable).Write("Some text message."));
        
        // Then we send a player-position packet.
        client.Send(PacketType.PlayerPosition.Get(Delivery.Reliable).Write(new Vector3(1, 2, 3)));
    }

    private static void HandleTextMessagePacket(Server server, ReadOnlyPacket packet, EndPoint clientEndPoint)
    {
        var message = packet.ReadString();
        Console.WriteLine($"Server received a text-message packet: {message}");
    }

    private static void HandlePlayerPositionPacket(Server server, ReadOnlyPacket packet, EndPoint clientEndPoint)
    {
        var position = packet.Read<Vector3>();
        Console.WriteLine($"Server received a player-position packet: {position}");
    }

    /// <summary>
    /// Extension that creates <see cref="Packet"/> from <see cref="PacketType"/> and returns it.
    /// </summary>
    private static Packet Get(this PacketType packetType, Delivery delivery) => Packet.Get(delivery, packetType.Id());

    /// <summary>
    /// Extension that converts <see cref="PacketType"/> to its underlying <see cref="ushort"/> value.
    /// </summary>
    private static ushort Id(this PacketType packetType) => (ushort) packetType;

    /// <summary>
    /// Defines all of the possible packet types that are used in the application.
    /// It is important to note that we are extending from <see cref="ushort"/> as
    /// packet IDs are <see cref="ushort"/> values.
    /// </summary>
    private enum PacketType : ushort
    {
        TextMessage,
        PlayerPosition,
    }
}
