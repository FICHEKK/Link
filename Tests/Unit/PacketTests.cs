using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class PacketTests
{
    [Test]
    public void Setting_max_size_to_lower_than_min_valid_value_throws() =>
        Assert.That(() => Packet.MaxSize = Packet.MinSize - 1, Throws.Exception);

    [Test]
    public void Setting_max_size_to_min_valid_value_does_not_throw() =>
        Assert.That(() => Packet.MaxSize = Packet.MinSize, Throws.Nothing);
    
    [Test]
    public void Packet_with_specified_delivery_should_have_size_2() =>
        Assert.That(Packet.Get(Delivery.Unreliable).Size, Is.EqualTo(2));
    
    [Test]
    public void Packet_with_specified_channel_should_have_size_2() =>
        Assert.That(Packet.Get(channelId: 123).Size, Is.EqualTo(2));
    
    [Test]
    public void Writing_null_to_packet_throws()
    {
        Assert.That(() => Packet.Get().Write(null), Throws.Exception);
        Assert.That(() => Packet.Get().WriteArray<int>(null), Throws.Exception);
    }
    
    [Test]
    public void Reading_string_that_was_written_produces_same_string([Values("", "Test")] string stringToWrite)
    {
        var packet = Packet.Get().Write(stringToWrite);
        var @string = new ReadOnlyPacket(packet.Buffer).ReadString();
        Assert.That(@string, Is.EqualTo(stringToWrite));
    }
}
