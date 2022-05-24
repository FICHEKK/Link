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
        Assert.That(() => new ReadOnlyPacket(_buffer).ReadString(), Throws.Exception);
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
    
    [Test]
    public void Reading_array_of_length_0_returns_same_instance()
    {
        var readOnlyPacket = new ReadOnlyPacket(_buffer);
        var emptyArray0 = readOnlyPacket.ReadArray<byte>(length: 0);
        var emptyArray1 = readOnlyPacket.ReadArray<byte>(length: 0);
        Assert.That(emptyArray0 == emptyArray1);
    }
    
    [Test]
    public void Reading_array_whose_length_read_from_buffer_is_negative_throws()
    {
        const int arrayLength = -1;
        _buffer.WriteVarInt(arrayLength);
        Assert.That(() => new ReadOnlyPacket(_buffer).ReadArray<byte>(), Throws.Exception);
    }
    
    [Test]
    public void Reading_array_whose_length_is_too_big_throws()
    {
        const int arrayLength = int.MaxValue;
        _buffer.WriteVarInt(arrayLength);
        Assert.That(() => new ReadOnlyPacket(_buffer).ReadArray<int>(), Throws.Exception);
    }
    
    [Test]
    public void Wrapping_empty_buffer_should_return_size_0()
    {
        // Write header.
        _buffer.WriteArray(new byte[Packet.HeaderSize]);
        Assert.That(new ReadOnlyPacket(_buffer).Size, Is.EqualTo(0));
    }
    
    [Test]
    public void Wrapping_full_buffer_should_return_size_equal_to_packet_max_size()
    {
        // Write header.
        _buffer.WriteArray(new byte[Packet.HeaderSize]);
        _buffer.WriteArray(new byte[Packet.MaxSize]);
        Assert.That(new ReadOnlyPacket(_buffer).Size, Is.EqualTo(Packet.MaxSize));
    }
    
    [Test]
    public void Channel_id_returns_2nd_byte_of_the_buffer()
    {
        const byte channelId = 100;
        _buffer.Bytes[1] = channelId;
        Assert.That(new ReadOnlyPacket(_buffer).ChannelId, Is.EqualTo(channelId));
    }
    
    [Test]
    public void Reading_string_works_properly([ValueSource(typeof(TestData), nameof(TestData.Strings))] string stringToWrite)
    {
        var packet = Packet.Get().Write(stringToWrite);
        var readString = new ReadOnlyPacket(packet.Buffer).ReadString();
        Assert.That(readString, Is.EqualTo(stringToWrite));
    }
}
