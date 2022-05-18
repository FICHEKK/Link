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
    }
    
    [Test]
    public void Reading_string_that_was_written_produces_same_string([Values("", "Test")] string stringToWrite)
    {
        var packet = Packet.Get().Write(stringToWrite);
        var @string = new ReadOnlyPacket(packet.Buffer).ReadString();
        Assert.That(@string, Is.EqualTo(stringToWrite));
    }
}
