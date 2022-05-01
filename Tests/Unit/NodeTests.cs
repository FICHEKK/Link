using Link.Nodes;
using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class NodeTests
{
    [Test]
    public void New_node_should_be_in_automatic_mode()
    {
        Assert.That(new Client().IsAutomatic);
        Assert.That(new Server().IsAutomatic);
    }
    
    [Test]
    public void Calling_tick_with_automatic_mode_throws()
    {
        Assert.That(() => new Client().Tick(), Throws.Exception);
        Assert.That(() => new Server().Tick(), Throws.Exception);
    }
    
    [Test]
    public void New_node_should_not_be_listening()
    {
        Assert.That(new Client().IsListening, Is.False);
        Assert.That(new Server().IsListening, Is.False);
    }
    
    [Test]
    public void New_node_should_have_no_connection_initializer()
    {
        Assert.That(new Client().ConnectionInitializer, Is.Null);
        Assert.That(new Server().ConnectionInitializer, Is.Null);
    }
    
    [Test]
    public void New_node_should_have_port_set_to_minus_1()
    {
        Assert.That(new Client().Port, Is.EqualTo(-1));
        Assert.That(new Server().Port, Is.EqualTo(-1));
    }
    
    [Test]
    public void New_node_should_have_min_latency_set_to_0()
    {
        Assert.That(new Client().MinLatency, Is.EqualTo(0));
        Assert.That(new Server().MinLatency, Is.EqualTo(0));
    }
    
    [Test]
    public void New_node_should_have_max_latency_set_to_0()
    {
        Assert.That(new Client().MaxLatency, Is.EqualTo(0));
        Assert.That(new Server().MaxLatency, Is.EqualTo(0));
    }
    
    [Test]
    public void New_node_should_have_packet_loss_set_to_0()
    {
        Assert.That(new Client().PacketLoss, Is.EqualTo(0));
        Assert.That(new Server().PacketLoss, Is.EqualTo(0));
    }
    
    [Test]
    public void New_node_should_have_receive_buffer_size_greater_than_0()
    {
        Assert.That(new Client().ReceiveBufferSize, Is.GreaterThan(0));
        Assert.That(new Server().ReceiveBufferSize, Is.GreaterThan(0));
    }
    
    [Test]
    public void New_node_should_have_send_buffer_size_greater_than_0()
    {
        Assert.That(new Client().SendBufferSize, Is.GreaterThan(0));
        Assert.That(new Server().SendBufferSize, Is.GreaterThan(0));
    }
}
