using System.Threading.Tasks;
using Link.Nodes;
using NUnit.Framework;

namespace Link.Tests.Integration;

/// <summary>
/// Tests the behavior of establishing a connection between client and server.
/// </summary>
[TestFixture]
public class ConnectTests
{
    private Server _server;

    [SetUp]
    public void Start_default_server()
    {
        _server = new Server();
        _server.Start(Config.Port);
    }

    [TearDown]
    public void Stop_default_server()
    {
        _server.Stop();
    }

    [Test]
    public async Task Server_with_no_validator_and_empty_slot_should_accept_client_connection()
    {
        using var client = new Client();
        client.Connect(Config.IpAddress, Config.Port);

        await Task.Delay(Config.NetworkDelay);
        Assert.That(client.IsConnected);
    }

    [Test]
    public async Task Client_that_does_not_pass_validation_should_not_be_connected()
    {
        _server.ConnectionValidator = (_, _) => false;

        using var client = new Client();
        client.Connect(Config.IpAddress, Config.Port);

        await Task.Delay(Config.NetworkDelay);
        Assert.That(!client.IsConnected);
    }
    
    [Test]
    public async Task Full_server_should_decline_client_connection()
    {
        // Accept only if empty.
        _server.ConnectionValidator = (_, _) => _server.ConnectionCount < 1;
        
        using var client1 = new Client();
        client1.Connect(Config.IpAddress, Config.Port);

        await Task.Delay(Config.NetworkDelay);
        Assert.That(client1.IsConnected);
        Assert.That(_server.ConnectionCount, Is.EqualTo(1));
        
        using var client2 = new Client();
        client2.Connect(Config.IpAddress, Config.Port);
        
        await Task.Delay(Config.NetworkDelay);
        Assert.That(!client2.IsConnected);
        Assert.That(_server.ConnectionCount, Is.EqualTo(1));
    }
    
    [Test]
    public async Task Server_should_receive_same_connect_packet_that_client_sent()
    {
        const int integerToWrite = 123;
        const string stringToWrite = "Test";
        var sameDataWasReceived = true;

        _server.ConnectionValidator = (connectPacket, _) =>
        {
            sameDataWasReceived = sameDataWasReceived && connectPacket.Read<int>() == integerToWrite;
            sameDataWasReceived = sameDataWasReceived && connectPacket.ReadString() == stringToWrite;
            return sameDataWasReceived;
        };

        using var client = new Client();
        client.Connect(Config.IpAddress, Config.Port, connectPacketFactory: writer => writer.Write(integerToWrite).Write(stringToWrite));

        await Task.Delay(Config.NetworkDelay);
        Assert.That(sameDataWasReceived);
    }
}
