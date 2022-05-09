using System.Collections.Generic;
using Link.Nodes;
using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class NodeTests
{
    private static IEnumerable<Node> Nodes
    {
        get
        {
            yield return new Client();
            yield return new Server();
        }
    }
    
    [Test]
    public void New_node_should_be_in_automatic_mode([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.IsAutomatic);
    
    [Test]
    public void Calling_tick_with_automatic_mode_throws([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.Tick, Throws.Exception);
    
    [Test]
    public void New_node_should_not_be_listening([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.IsListening, Is.False);
    
    [Test]
    public void New_node_should_have_no_connection_initializer([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.ConnectionInitializer, Is.Null);
    
    [Test]
    public void New_node_should_have_port_set_to_minus_1([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.Port, Is.EqualTo(-1));
    
    [Test]
    public void New_node_should_have_min_latency_set_to_0([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.MinLatency, Is.EqualTo(0));
    
    [Test]
    public void New_node_should_have_max_latency_set_to_0([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.MaxLatency, Is.EqualTo(0));
    
    [Test]
    public void New_node_should_have_equal_min_and_max_latencies([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.MinLatency, Is.EqualTo(node.MaxLatency));
    
    [Test]
    public void New_node_should_have_packet_loss_set_to_0([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.PacketLoss, Is.EqualTo(0));
    
    [Test]
    public void New_node_should_have_receive_buffer_size_greater_than_0([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.ReceiveBufferSize, Is.GreaterThan(0));
    
    [Test]
    public void New_node_should_have_send_buffer_size_greater_than_0([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(node.SendBufferSize, Is.GreaterThan(0));

    [Test]
    public void Setting_min_latency_to_negative_throws([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(() => node.MinLatency = -1, Throws.Exception);
    
    [Test]
    public void Setting_max_latency_to_negative_throws([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(() => node.MaxLatency = -1, Throws.Exception);
    
    [Test]
    public void Setting_min_latency_to_greater_than_max_latency_does_not_throw([ValueSource(nameof(Nodes))] Node node) =>
        Assert.That(() => node.MinLatency = node.MaxLatency + 1, Throws.Nothing);
    
    [Test]
    public void Setting_max_latency_to_lower_than_min_latency_does_not_throw([ValueSource(nameof(Nodes))] Node node)
    {
        node.MinLatency = node.MaxLatency = 1000;
        Assert.That(() => node.MaxLatency = node.MinLatency - 1, Throws.Nothing);
    }

    [Test]
    public void Setting_min_latency_to_greater_than_max_latency_also_sets_max_latency([ValueSource(nameof(Nodes))] Node node)
    {
        node.MinLatency = node.MaxLatency + 1;
        Assert.That(node.MinLatency, Is.EqualTo(node.MaxLatency));
    }
    
    [Test]
    public void Setting_max_latency_to_lower_than_min_latency_also_sets_min_latency([ValueSource(nameof(Nodes))] Node node)
    {
        node.MinLatency = node.MaxLatency = 1000;
        node.MaxLatency = node.MinLatency - 1;
        Assert.That(node.MinLatency, Is.EqualTo(node.MaxLatency));
    }
}
