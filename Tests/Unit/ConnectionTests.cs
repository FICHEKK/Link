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
        _connection = new Connection(node: null, remoteEndPoint: null, Connection.State.Disconnected);

    [Test]
    public void New_connection_should_have_rtt_set_to_minus_1() =>
        Assert.That(_connection.RoundTripTime, Is.EqualTo(-1));
    
    [Test]
    public void New_connection_should_have_smooth_rtt_set_to_minus_1() =>
        Assert.That(_connection.SmoothRoundTripTime, Is.EqualTo(-1));
    
    [Test]
    public void New_connection_should_have_rtt_deviation_set_to_minus_1() =>
        Assert.That(_connection.RoundTripTimeDeviation, Is.EqualTo(-1));
    
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
    public void Getting_channel_in_one_of_the_reserved_slots_always_returns_non_null()
    {
        foreach (var delivery in (Delivery[]) Enum.GetValues(typeof(Delivery)))
        {
            Assert.That(_connection[(byte) delivery], Is.Not.Null);
        }
    }

    [Test]
    public void New_connection_should_have_number_of_channels_equal_to_number_of_delivery_methods()
    {
        var deliveryCount = Enum.GetValues(typeof(Delivery)).Length;
        Assert.That(_connection.ChannelCount, Is.EqualTo(deliveryCount));
    }
    
    [Test]
    public void Setting_period_duration_to_less_than_0_throws()
    {
        Assert.That(() => _connection.PeriodDuration = 1, Throws.Nothing);
        Assert.That(() => _connection.PeriodDuration = 0, Throws.Nothing);
        
        // However, negative should throw.
        Assert.That(() => _connection.PeriodDuration = -1, Throws.Exception);
    }
    
    [Test]
    public void Setting_timeout_duration_to_less_than_minus_1_throws()
    {
        Assert.That(() => _connection.TimeoutDuration = 1, Throws.Nothing);
        Assert.That(() => _connection.TimeoutDuration = 0, Throws.Nothing);
        Assert.That(() => _connection.TimeoutDuration = -1, Throws.Nothing);
        
        // However, less than -1 should throw.
        Assert.That(() => _connection.TimeoutDuration = -2, Throws.Exception);
    }
}
