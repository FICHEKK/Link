using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class PacketReaderTests
{
    private Packet _packet;

    [SetUp]
    public void Get_empty_packet() => _packet = Packet.Get();

    [TearDown]
    public void Return_packet() => _packet.Return();

    [Test]
    public void Reading_empty_packet_throws()
    {
        Assert.That(() => new PacketReader(_packet).Read<int>(), Throws.Exception);
        Assert.That(() => new PacketReader(_packet).ReadSlice<int>(length: 1), Throws.Exception);
        Assert.That(() => new PacketReader(_packet).ReadArray<int>(), Throws.Exception);
    }

    [Test]
    public void Reading_value_in_range_does_not_throw<T>([ValueSource(typeof(TestData), nameof(TestData.Values))] T value) where T : unmanaged
    {
        _packet.Write(value);
        Assert.That(() => new PacketReader(_packet).Read<T>(), Throws.Nothing);
    }

    [Test]
    public void Reading_slice_in_range_does_not_throw<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] array) where T : unmanaged
    {
        _packet.WriteSlice(array, start: 0, length: array.Length);
        Assert.That(() => new PacketReader(_packet).ReadSlice<T>(array.Length), Throws.Nothing);
    }

    [Test]
    public void Reading_array_in_range_does_not_throw<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] array) where T : unmanaged
    {
        _packet.WriteArray(array);
        Assert.That(() => new PacketReader(_packet).ReadArray<T>(), Throws.Nothing);
    }

    [Test]
    public void Reading_value_out_of_bounds_throws<T>([ValueSource(typeof(TestData), nameof(TestData.Values))] T value) where T : unmanaged
    {
        _packet.Write(value);

        Assert.That(() =>
        {
            var reader = new PacketReader(_packet);
            reader.Read<byte>();
            reader.Read<T>();
        }, Throws.Exception);
    }
    
    [Test]
    public void Reading_slice_out_of_bounds_throws<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] array) where T : unmanaged
    {
        _packet.WriteSlice(array, start: 0, length: array.Length);
        
        Assert.That(() =>
        {
            var reader = new PacketReader(_packet);
            reader.ReadSlice<T>(array.Length + 1);
        }, Throws.Exception);
    }
}
