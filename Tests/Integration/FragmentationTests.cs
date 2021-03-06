using System.Collections.Generic;
using System.Threading.Tasks;
using Link.Nodes;
using NUnit.Framework;

namespace Link.Tests.Integration;

[TestFixture]
public class FragmentationTests
{
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
    public async Task Fragmented_packet_should_be_delivered_fully()
    {
        var receivedArray = (int[]) null;
        _server.AddHandler(args => receivedArray = args.Packet.Read<int[]>());

        var sentArray = CreateIntArray(size: 1024);
        _client.Send(Packet.Get(Delivery.Reliable).Write(sentArray));
        
        await Task.Delay(Config.NetworkDelay);
        Assert.That(receivedArray, Is.EqualTo(sentArray));
    }
    
    [Test]
    public async Task Multiple_fragmented_packets_should_be_delivered_fully()
    {
        var receivedArrays = new List<int[]>();
        _server.AddHandler(args => receivedArrays.Add(args.Packet.Read<int[]>()));

        var sentArray0 = CreateIntArray(size: 256);
        var sentArray1 = CreateIntArray(size: 512);
        var sentArray2 = CreateIntArray(size: 1024);
        
        _client.Send(Packet.Get(Delivery.Reliable).Write(sentArray0));
        _client.Send(Packet.Get(Delivery.Reliable).Write(sentArray1));
        _client.Send(Packet.Get(Delivery.Reliable).Write(sentArray2));
        
        await Task.Delay(Config.NetworkDelay);
        
        Assert.That(receivedArrays[0], Is.EqualTo(sentArray0));
        Assert.That(receivedArrays[1], Is.EqualTo(sentArray1));
        Assert.That(receivedArrays[2], Is.EqualTo(sentArray2));
    }

    private static int[] CreateIntArray(int size)
    {
        var array = new int[size];
            
        for (var i = 0; i < size; i++)
            array[i] = i;
            
        return array;
    }
}
