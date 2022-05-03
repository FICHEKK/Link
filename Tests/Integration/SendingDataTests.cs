using System.Threading.Tasks;
using Link.Nodes;
using NUnit.Framework;

namespace Link.Tests.Integration;

[TestFixture]
public class SendingDataTests
{
    private const string ExampleString = "Hello world!";
    
    private Server _server;
    private Client _client;

    [SetUp]
    public void Start_default_server()
    {
        (_server = new Server()).Start(Config.Port);
        (_client = new Client()).Connect(Config.IpAddress, Config.Port);

        while (!_client.IsConnected) { }
    }

    [TearDown]
    public void Stop_default_server()
    {
        _client.Disconnect();
        _server.Stop();
    }

    [Test]
    public async Task Sending_string_works_as_intended()
    {
        string stringReceivedOnServer = null;
        _server.PacketReceived += (packet, _) => stringReceivedOnServer = packet.ReadString();

        _client.Send(Packet.Get(Delivery.Reliable).Write(ExampleString));
        await Task.Delay(Config.NetworkDelay);
        
        Assert.That(stringReceivedOnServer, Is.EqualTo(ExampleString));
    }
    
    [Test]
    public async Task All_packet_listeners_should_be_called_and_get_same_packet_data()
    {
        string stringReceivedByFirstListener = null;
        string stringReceivedBySecondListener = null;
        
        _server.PacketReceived += (packet, _) => stringReceivedByFirstListener = packet.ReadString();
        _server.PacketReceived += (packet, _) => stringReceivedBySecondListener = packet.ReadString();

        _client.Send(Packet.Get(Delivery.Reliable).Write(ExampleString));
        await Task.Delay(Config.NetworkDelay);
        
        Assert.That(stringReceivedByFirstListener, Is.EqualTo(ExampleString));
        Assert.That(stringReceivedBySecondListener, Is.EqualTo(ExampleString));
    }
    
    [Test]
    public void Sending_packet_with_size_greater_than_max_size_should_fail()
    {
        var packet = Packet.Get().WriteSlice(new byte[1024], start: 0, length: 1024);
        
        Assert.That(_client.Send(packet, _client.Connection.RemoteEndPoint), Is.True);
        Packet.MaxSize = 1000;
        Assert.That(_client.Send(packet, _client.Connection.RemoteEndPoint), Is.False);
    }
}
