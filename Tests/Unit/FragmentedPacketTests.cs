using Link.Channels;
using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class FragmentedPacketTests
{
    private FragmentedPacket _fragmentedPacket;

    [SetUp]
    public void Create_new_fragmented_packet() =>
        _fragmentedPacket = new FragmentedPacket(headerSize: 0, bodySize: 1, footerSize: 0);

    [Test]
    public void New_fragmented_packet_should_not_be_reassembled() =>
        Assert.That(_fragmentedPacket.ReassembledPacket, Is.Null);
    
    [Test]
    public void Adding_null_fragment_throws() =>
        Assert.That(() => _fragmentedPacket.Add(null, fragmentNumber: 0, isLastFragment: true), Throws.Exception);

    [Test]
    public void Adding_negative_fragment_number_throws() =>
        Assert.That(() => _fragmentedPacket.Add(Buffer.Get(), fragmentNumber: -1, isLastFragment: true), Throws.Exception);

    [Test]
    public void Adding_one_and_last_fragment_should_produce_reassembled_packet()
    {
        _fragmentedPacket.Add(Buffer.Get(), fragmentNumber: 0, isLastFragment: true);
        Assert.That(_fragmentedPacket.ReassembledPacket, Is.Not.Null);
    }

    [Test]
    public void Fragment_can_be_added_only_once()
    {
        var isFirstAdded = _fragmentedPacket.Add(Buffer.Get(), fragmentNumber: 0, isLastFragment: true);
        var isSecondAdded = _fragmentedPacket.Add(Buffer.Get(), fragmentNumber: 0, isLastFragment: true);

        Assert.That(isFirstAdded, Is.True);
        Assert.That(isSecondAdded, Is.False);
    }
}
