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
    public void Start_server_and_connect_client()
    {
        (_server = new Server()).Start(Config.Port);
        (_client = new Client()).Connect(Config.IpAddress, Config.Port);

        while (!_client.IsConnected) { }
    }

    [TearDown]
    public void Disconnect_client_and_stop_server()
    {
        _client.Disconnect();
        _server.Stop();
    }

    [Test]
    public async Task Sending_string_works_as_intended()
    {
        string stringReceivedOnServer = null;
        _server.AddHandler(args => stringReceivedOnServer = args.Packet.Read<string>());

        _client.Send(Packet.Get(Delivery.Reliable).Write<string>(ExampleString));
        await Task.Delay(Config.NetworkDelay);
        
        Assert.That(stringReceivedOnServer, Is.EqualTo(ExampleString));
    }
    
    [Test]
    public void Sending_packet_with_size_greater_than_max_size_throws()
    {
        var shouldThrowArray = new byte[Packet.MaxSize + 1];
        Assert.That(() => _client.Send(Packet.Get(channelId: 0).Write(shouldThrowArray), _client.Connection!.RemoteEndPoint), Throws.Exception);
    }
}
