using System.Net;
using Link.Nodes;

namespace Link.Examples._017_Packet_Extensions;

/// <summary>
/// This example demonstrates how to extend the functionality of <see cref="Packet"/> and
/// <see cref="ReadOnlyPacket"/> in order to be able to read and write custom data-types.
/// </summary>
public static class PacketExtensions
{
    private const string IpAddress = "127.0.0.1";
    private const ushort Port = 7777;

    public static void Main()
    {
        using var server = new Server();
        server.AddHandler(ReceivePeople);
        server.Start(Port);
        
        using var client = new Client();
        client.Connected += SendPeople;
        client.Connect(IpAddress, Port);
        
        Console.ReadKey();
    }
    
    private static void SendPeople(Client.ConnectedEventArgs args)
    {
        var peoplePacket = Packet.Get(Delivery.Reliable)
            .WritePerson(new Person("Bob", "Ross", age: 52))
            .WritePerson(new Person("John", "Smith", age: 69))
            .WritePerson(new Person("Steve", "Irwin", age: 44));

        args.Client.Send(peoplePacket);
    }

    private static void ReceivePeople(Server.ReceiveArgs args)
    {
        Console.WriteLine("Server received the following people:");
        Console.WriteLine(args.Packet.ReadPerson());
        Console.WriteLine(args.Packet.ReadPerson());
        Console.WriteLine(args.Packet.ReadPerson());
    }

    /// <summary>
    /// Extends a packet with the ability to write an instance of <see cref="Person"/> class.
    /// Note that this method also returns packet so that it can be chained with other write methods.
    /// </summary>
    private static Packet WritePerson(this Packet packet, Person person)
    {
        packet.Write(person.FirstName);
        packet.Write(person.LastName);
        packet.Write(person.Age);
        return packet;
    }

    /// <summary>
    /// Extends read-only packet with ability to read an instance of <see cref="Person"/> class.
    /// </summary>
    private static Person ReadPerson(this ReadOnlyPacket packet)
    {
        var firstName = packet.ReadString();
        var lastName = packet.ReadString();
        var age = packet.Read<int>();
        return new Person(firstName, lastName, age);
    }
    
    private class Person
    {
        public string FirstName { get; }
        public string LastName { get; }
        public int Age { get; }
        
        public Person(string firstName, string lastName, int age)
        {
            FirstName = firstName;
            LastName = lastName;
            Age = age;
        }

        public override string ToString() => $"{FirstName} {LastName}, age: {Age}";
    }
}
