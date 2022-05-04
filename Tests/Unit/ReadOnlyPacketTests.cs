using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class ReadOnlyPacketTests
{
    private Buffer _buffer;

    [SetUp]
    public void Get_empty_buffer() => _buffer = Buffer.Get();

    [TearDown]
    public void Return_buffer() => _buffer.Return();

    [Test]
    public void Reading_empty_packet_throws()
    {
        Assert.That(() => new ReadOnlyPacket(_buffer).Read<int>(), Throws.Exception);
        Assert.That(() => new ReadOnlyPacket(_buffer).ReadArray<int>(), Throws.Exception);
    }

    [Test]
    public void Reading_value_in_range_does_not_throw<T>([ValueSource(typeof(TestData), nameof(TestData.Values))] T value) where T : unmanaged
    {
        _buffer.Write(value);
        Assert.That(() => new ReadOnlyPacket(_buffer).Read<T>(), Throws.Nothing);
    }

    [Test]
    public void Reading_array_in_range_does_not_throw<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] array) where T : unmanaged
    {
        _buffer.WriteArray(array);
        Assert.That(() => new ReadOnlyPacket(_buffer).ReadArray<T>(array.Length), Throws.Nothing);
    }

    [Test]
    public void Reading_value_out_of_bounds_throws<T>([ValueSource(typeof(TestData), nameof(TestData.Values))] T value) where T : unmanaged
    {
        _buffer.Write(value);

        Assert.That(() =>
        {
            var packet = new ReadOnlyPacket(_buffer);
            packet.Read<byte>();
            packet.Read<T>();
        }, Throws.Exception);
    }
    
    [Test]
    public void Reading_array_out_of_bounds_throws<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] array) where T : unmanaged
    {
        _buffer.WriteArray(array, start: 0, length: array.Length);
        
        Assert.That(() =>
        {
            var packet = new ReadOnlyPacket(_buffer);
            packet.ReadArray<T>(array.Length + 1);
        }, Throws.Exception);
    }
}
