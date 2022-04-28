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

        while (!_client.IsConnected)
        {
            // Simple wait...
        }
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
        var wasReceivedOnServer = false;
        
        _server.PacketReceived += (reader, _) =>
        {
            wasReceivedOnServer = true;
            Assert.That(reader.ReadString(), Is.EqualTo(ExampleString));
        };

        _client.Send(Packet.Get(Delivery.Reliable).Write(ExampleString));
        await Task.Delay(Config.NetworkDelay);
        
        _server.Tick();
        Assert.That(wasReceivedOnServer);
    }
    
    [Test]
    public async Task All_packet_listeners_should_be_called_and_get_same_packet_data()
    {
        var firstListenerWasCalled = false;
        var secondListenerWasCalled = false;
        
        _server.PacketReceived += (reader, _) =>
        {
            firstListenerWasCalled = true;
            Assert.That(reader.ReadString(), Is.EqualTo(ExampleString));
        };
        
        _server.PacketReceived += (reader, _) =>
        {
            secondListenerWasCalled = true;
            Assert.That(reader.ReadString(), Is.EqualTo(ExampleString));
        };

        _client.Send(Packet.Get(Delivery.Reliable).Write(ExampleString));
        await Task.Delay(Config.NetworkDelay);
        
        _server.Tick();
        Assert.That(firstListenerWasCalled);
        Assert.That(secondListenerWasCalled);
    }
}
