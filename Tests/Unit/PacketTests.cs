using System.Text;
using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class PacketTests
{
    [Test]
    public void Fresh_packet_should_have_size_0()
    {
        Assert.That(Packet.Get(Delivery.Unreliable).Size, Is.EqualTo(0));
        Assert.That(Packet.Get(channelId: 123).Size, Is.EqualTo(0));
    }
    
    [Test]
    public void Writing_null_to_packet_throws()
    {
        Assert.That(() => Packet.Get().Write(null), Throws.Exception);
        Assert.That(() => Packet.Get().WriteArray<int>(null), Throws.Exception);
        Assert.That(() => Packet.Get().WriteArray<int>(null, start: 0, length: 0), Throws.Exception);
    }
    
    [Test]
    public void Reading_string_that_was_written_produces_same_string([ValueSource(typeof(TestData), nameof(TestData.Strings))] string stringToWrite)
    {
        var packet = Packet.Get().Write(stringToWrite);
        var @string = new ReadOnlyPacket(packet.Buffer).ReadString();
        Assert.That(@string, Is.EqualTo(stringToWrite));
    }

    [Test]
    public void Getting_byte_at_negative_index_throws() => Assert.That(() =>
    {
        var packet = Packet.Get(Delivery.Unreliable);
        var value = packet[-1];
    }, Throws.Exception);

    [Test]
    public void Setting_byte_at_negative_index_throws() => Assert.That(() =>
    {
        var packet = Packet.Get(Delivery.Unreliable);
        packet[-1] = byte.MaxValue;
    }, Throws.Exception);

    [Test]
    public void Getting_byte_at_index_equal_to_or_greater_than_packet_size_throws() => Assert.That(() =>
    {
        var packet = Packet.Get(Delivery.Unreliable);
        var value = packet[0];
    }, Throws.Exception);

    [Test]
    public void Setting_byte_at_index_equal_to_or_greater_than_packet_size_throws() => Assert.That(() =>
    {
        var packet = Packet.Get(Delivery.Unreliable);
        packet[0] = byte.MaxValue;
    }, Throws.Exception);
    
    [Test]
    public void Getting_byte_at_valid_index_does_not_throw() => Assert.That(() =>
    {
        var packet = Packet.Get(Delivery.Unreliable);
        packet.Write(byte.MinValue);
        var value = packet[0];
    }, Throws.Nothing);

    [Test]
    public void Setting_byte_at_valid_index_does_not_throw() => Assert.That(() =>
    {
        var packet = Packet.Get(Delivery.Unreliable);
        packet.Write(byte.MinValue);
        packet[0] = byte.MaxValue;
    }, Throws.Nothing);
    
    [Test]
    public void Default_encoding_should_be_UTF8() =>
        Assert.That(Packet.Encoding, Is.EqualTo(Encoding.UTF8));

    [Test]
    public void Setting_encoding_to_null_should_throw() =>
        Assert.That(() => Packet.Encoding = null, Throws.Exception);
    
    [Test]
    public void Allocation_count_should_be_at_least_1()
    {
        var packet = Packet.Get();
        Assert.That(Packet.AllocationCount, Is.GreaterThanOrEqualTo(1));
        packet.Return();
    }
    
    [Test]
    public void Unwritten_bytes_for_new_packet_should_be_equal_to_max_packet_size()
    {
        var packet = Packet.Get(Delivery.Unreliable);
        Assert.That(packet.UnwrittenBytes, Is.EqualTo(Packet.MaxSize));
        packet.Return();
    }
    
    [Test]
    public void Unwritten_bytes_should_return_0_if_packet_is_full()
    {
        var packet = Packet.Get(Delivery.Unreliable).WriteArray(new byte[Packet.MaxSize], writeLength: false);
        Assert.That(packet.UnwrittenBytes, Is.EqualTo(0));
        packet.Return();
    }
    
    [Test]
    public void Unwritten_bytes_should_return_negative_if_packet_should_be_fragmented()
    {
        var packet = Packet.Get(Delivery.Unreliable).WriteArray(new byte[Packet.MaxSize + 1]);
        Assert.That(packet.UnwrittenBytes, Is.Negative);
        packet.Return();
    }
}
