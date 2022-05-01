using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class PacketTests
{
    private Packet _packet;

    [SetUp]
    public void Get_packet() =>_packet = Packet.Get();

    [TearDown]
    public void Return_packet() => _packet.Return();
    
    [Test]
    public void Fresh_packet_size_should_be_zero() =>
        Assert.That(_packet.Size, Is.EqualTo(0));

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
    public void Writing_to_packet_in_pool_throws()
    {
        var packet = Packet.Get();
        packet.Return();
        Assert.That(() => packet.Write(0), Throws.Exception);
    }
    
    [Test]
    public void Reading_from_packet_in_pool_throws()
    {
        var packet = Packet.Get();
        packet.Return();
        Assert.That(() => new PacketReader(packet).Read<int>(), Throws.Exception);
    }

    [Test]
    public void Returning_packet_out_of_pool_successfully_returns()
    {
        var packet = Packet.Get();
        Assert.That(packet.Return(), Is.True);
    }
    
    [Test]
    public void Returning_packet_in_pool_does_not_return()
    {
        var packet = Packet.Get();
        packet.Return();
        Assert.That(packet.Return(), Is.False);
    }
    
    [Test]
    public void Returning_packet_that_is_too_big_does_not_return()
    {
        var packet = Packet.Get();
        packet.WriteArray(new byte[100_000]);
        Assert.That(packet.Return(), Is.False);
    }
    
    [Test]
    public void Writing_null_to_packet_throws()
    {
        Assert.That(() => _packet.Write(null), Throws.Exception);
        Assert.That(() => _packet.WriteArray<int>(null), Throws.Exception);
        Assert.That(() => _packet.WriteSlice<int>(null, start: 0, length: 0), Throws.Exception);
    }
    
    [Test]
    public void Reading_string_that_was_written_produces_same_string([Values("", "Test")] string stringToWrite)
    {
        _packet.Write(stringToWrite);
        var @string = new PacketReader(_packet).ReadString();
        Assert.That(@string, Is.EqualTo(stringToWrite));
    }
    
    [Test]
    public void Reading_value_that_was_written_produces_same_value<T>([ValueSource(typeof(TestData), nameof(TestData.Values))] T valueToWrite)
        where T : unmanaged
    {
        _packet.Write(valueToWrite);
        var value = new PacketReader(_packet).Read<T>();
        Assert.That(value, Is.EqualTo(valueToWrite));
    }

    [Test]
    public void Reading_array_that_was_written_produces_same_array<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] arrayToWrite)
        where T : unmanaged
    {
        _packet.WriteArray(arrayToWrite);
        var array = new PacketReader(_packet).ReadArray<T>();
        Assert.That(array, Is.EqualTo(arrayToWrite));
    }
    
    [Test]
    public void Reading_slice_that_was_written_produces_same_slice<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] sliceToWrite)
        where T : unmanaged
    {
        _packet.WriteSlice(sliceToWrite, start: 0, length: sliceToWrite.Length);
        var slice = new PacketReader(_packet).ReadSlice<T>(sliceToWrite.Length);
        Assert.That(slice, Is.EqualTo(sliceToWrite));
    }
}
