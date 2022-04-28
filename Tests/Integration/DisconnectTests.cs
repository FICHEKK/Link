using System.Linq;
using System.Threading.Tasks;
using Link.Nodes;
using NUnit.Framework;

namespace Link.Tests.Integration;

[TestFixture]
public class DisconnectTests
{
    [Test]
    public async Task Disposed_client_should_properly_disconnect()
    {
        using var server = new Server();
        server.Start(Config.Port);

        using (var client = new Client())
        {
            client.Connect(Config.IpAddress, Config.Port);
            await Task.Delay(Config.NetworkDelay);
            
            Assert.That(client.IsConnected);
            Assert.That(server.ConnectionCount, Is.EqualTo(1));
        }

        await Task.Delay(Config.NetworkDelay);
        Assert.That(server.ConnectionCount, Is.EqualTo(0));
    }
    
    [Test]
    public async Task Server_kick_properly_disconnects_client()
    {
        using var server = new Server();
        server.Start(Config.Port);

        using var client = new Client();
        client.Connect(Config.IpAddress, Config.Port);

        await Task.Delay(Config.NetworkDelay);
        Assert.That(client.IsConnected);
        Assert.That(server.ConnectionCount, Is.EqualTo(1));
        
        server.Kick(server.EndPoints.First());
        
        await Task.Delay(Config.NetworkDelay);
        Assert.That(!client.IsConnected);
        Assert.That(server.ConnectionCount, Is.EqualTo(0));
    }
}
