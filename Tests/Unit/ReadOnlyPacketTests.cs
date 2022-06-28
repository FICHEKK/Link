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
        Assert.That(() => new ReadOnlyPacket(_buffer).Read<string>(), Throws.Exception);
        Assert.That(() => new ReadOnlyPacket(_buffer).ReadArray<int>(), Throws.Exception);
    }

    [Test]
    public void Reading_value_in_range_does_not_throw()
    {
        var bufferWithByte = Buffer.Get();
        bufferWithByte.Write((byte) 0);
        Assert.That(() => new ReadOnlyPacket(bufferWithByte).Read<byte>(), Throws.Nothing);
        
        var bufferWithShort = Buffer.Get();
        bufferWithShort.Write((short) 0);
        Assert.That(() => new ReadOnlyPacket(bufferWithShort).Read<short>(), Throws.Nothing);
        
        var bufferWithInt = Buffer.Get();
        bufferWithInt.Write(0);
        Assert.That(() => new ReadOnlyPacket(bufferWithInt).Read<int>(), Throws.Nothing);
        
        var bufferWithLong = Buffer.Get();
        bufferWithLong.Write((long) 0);
        Assert.That(() => new ReadOnlyPacket(bufferWithLong).Read<long>(), Throws.Nothing);
    }

    [Test]
    public void Reading_array_in_range_does_not_throw<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] array)
    {
        var buffer = Packet.Get().WriteArray(array).Buffer;
        Assert.That(() => new ReadOnlyPacket(buffer).ReadArray<T>(array.Length), Throws.Nothing);
    }

    [Test]
    public void Reading_value_out_of_bounds_throws<T>([ValueSource(typeof(TestData), nameof(TestData.Values))] T value)
    {
        var buffer = Packet.Get().Write(value).Buffer;

        Assert.That(() =>
        {
            var packet = new ReadOnlyPacket(buffer);
            packet.Read<byte>();
            packet.Read<T>();
        }, Throws.Exception);
    }
    
    [Test]
    public void Reading_array_out_of_bounds_throws<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] array) where T : unmanaged
    {
        var buffer = Packet.Get().WriteArray(array, writeLength: false).Buffer;
        
        Assert.That(() =>
        {
            var packet = new ReadOnlyPacket(buffer);
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
    public void Wrapping_new_packet_buffer_should_return_size_0()
    {
        var buffer = Packet.Get(Delivery.Unreliable).Buffer;
        Assert.That(new ReadOnlyPacket(buffer).Size, Is.EqualTo(0));
    }
    
    [Test]
    public void Wrapping_full_buffer_should_return_size_equal_to_packet_max_size()
    {
        var buffer = Packet.Get(Delivery.Unreliable).WriteArray(new byte[Packet.MaxSize], writeLength: false).Buffer;
        Assert.That(new ReadOnlyPacket(buffer).Size, Is.EqualTo(Packet.MaxSize));
    }
    
    [Test]
    public void Channel_id_returns_2nd_byte_of_the_buffer()
    {
        const byte channelId = 100;
        _buffer.Bytes[1] = channelId;
        Assert.That(new ReadOnlyPacket(_buffer).ChannelId, Is.EqualTo(channelId));
    }
    
    [Test]
    public void Packet_id_returns_id_that_was_originally_defined()
    {
        const ushort packetId = ushort.MaxValue - 1;
        var packet = Packet.Get(Delivery.Unreliable, packetId);
        
        Assert.That(new ReadOnlyPacket(packet.Buffer).Id, Is.EqualTo(packetId));
    }
    
    [Test]
    public void Unread_bytes_returns_correct_value()
    {
        var buffer = Buffer.Get();
        buffer.Write(1);
        buffer.Write(2);
        buffer.Write(3);

        var packet = new ReadOnlyPacket(buffer);
        Assert.That(packet.UnreadBytes, Is.EqualTo(sizeof(int) * 3));

        packet.Read<int>();
        Assert.That(packet.UnreadBytes, Is.EqualTo(sizeof(int) * 2));
        
        packet.Read<int>();
        Assert.That(packet.UnreadBytes, Is.EqualTo(sizeof(int) * 1));
        
        packet.Read<int>();
        Assert.That(packet.UnreadBytes, Is.EqualTo(sizeof(int) * 0));
    }
    
    [Test]
    public void Passing_packet_to_method_and_returning_does_not_break_reading_flow()
    {
        var buffer = Buffer.Get();
        buffer.Write(1);
        buffer.Write(2);
        buffer.Write(3);
        buffer.Write(4);
        
        var packet = new ReadOnlyPacket(buffer);
        Assert.That(packet.Read<int>(), Is.EqualTo(1));
        MethodThatReadsFromPacket(packet);
        Assert.That(packet.Read<int>(), Is.EqualTo(4));

        void MethodThatReadsFromPacket(ReadOnlyPacket packet)
        {
            Assert.That(packet.Read<int>(), Is.EqualTo(2));
            Assert.That(packet.Read<int>(), Is.EqualTo(3));
        }
    }
    
    [Test]
    public void Reading_string_works_properly([ValueSource(typeof(TestData), nameof(TestData.Strings))] string stringToWrite)
    {
        var buffer = Packet.Get().Write<string>(stringToWrite).Buffer;
        var readString = new ReadOnlyPacket(buffer).Read<string>();
        Assert.That(readString, Is.EqualTo(stringToWrite));
    }
    
    [Test]
    public void Getting_byte_at_negative_index_throws() => Assert.That(() =>
    {
        var packet = new ReadOnlyPacket(Buffer.Get());
        var value = packet[-1];
    }, Throws.Exception);
    
    [Test]
    public void Getting_byte_at_index_equal_to_or_greater_than_packet_size_throws() => Assert.That(() =>
    {
        var packet = new ReadOnlyPacket(Packet.Get(Delivery.Unreliable).Buffer);
        var value = packet[0];
    }, Throws.Exception);
    
    [Test]
    public void Getting_byte_at_valid_index_does_not_throw() => Assert.That(() =>
    {
        var packet = new ReadOnlyPacket(Packet.Get(Delivery.Unreliable).Write<byte>(0).Buffer);
        var value = packet[0];
    }, Throws.Nothing);
    
    [Test]
    public void Getting_byte_at_valid_index_returns_proper_value()
    {
        const byte randomByteValue = 17;
        var packet = new ReadOnlyPacket(Packet.Get(Delivery.Unreliable).Write(randomByteValue).Buffer);
        Assert.That(packet[0], Is.EqualTo(randomByteValue));
    }
}
