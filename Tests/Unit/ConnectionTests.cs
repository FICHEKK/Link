using System;
using Link.Channels;
using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class ConnectionTests
{
    private Connection _connection;
    
    [SetUp]
    public void Create_new_connection() =>
        _connection = new Connection(null, null, Connection.State.Disconnected);
    
    [Test]
    public void Setting_null_channel_throws() =>
        Assert.That(() => _connection[100] = null, Throws.Exception);

    [Test]
    public void Setting_channel_in_the_slot_that_is_taken_throws()
    {
        _connection[100] = new UnreliableChannel(_connection);
        Assert.That(() => _connection[100] = new UnreliableChannel(_connection), Throws.Exception);
    }
    
    [Test]
    public void Setting_channel_in_one_of_the_reserved_slots_throws()
    {
        Assert.That(() => _connection[(byte) Delivery.Unreliable] = new UnreliableChannel(_connection), Throws.Exception);
        Assert.That(() => _connection[byte.MaxValue] = new UnreliableChannel(_connection), Throws.Exception);
    }

    [Test]
    public void New_connection_should_have_number_of_channels_equal_to_number_of_delivery_methods()
    {
        var deliveryCount = Enum.GetValues(typeof(Delivery)).Length;
        Assert.That(_connection.ChannelCount, Is.EqualTo(deliveryCount));
    }
}
