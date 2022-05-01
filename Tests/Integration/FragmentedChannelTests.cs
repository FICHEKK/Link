using System.Threading.Tasks;
using Link.Nodes;
using NUnit.Framework;

namespace Link.Tests.Integration;

[TestFixture]
public class FragmentedChannelTests
{
    [Test]
    public async Task Single()
    {
        using var server = new Server();
        server.Start(Config.Port);
        
        var receivedArray = (int[]) null;
        server.PacketReceived += (reader, _) => receivedArray = reader.ReadArray<int>();

        using var client = new Client();
        client.Connect(Config.IpAddress, Config.Port);

        while (!client.IsConnected) { }

        const int arraySize = 1024;
        var sentArray = CreateIntArray(arraySize);
        client.Send(Packet.Get(Delivery.Fragmented).WriteArray(sentArray));

        await Task.Delay(Config.NetworkDelay);
        
        Assert.That(receivedArray, Is.Not.Null);
        Assert.That(receivedArray.Length, Is.EqualTo(sentArray.Length));
        Assert.That(receivedArray, Is.EqualTo(sentArray));

        int[] CreateIntArray(int size)
        {
            var array = new int[size];

            for (var i = 0; i < size; i++)
                array[i] = i;

            return array;
        }
    }
}
