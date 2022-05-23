using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class BufferTests
{
    private Buffer _buffer;

    [SetUp]
    public void Clear_buffer() => _buffer = Buffer.Get();

    [TearDown]
    public void Return_buffer() => _buffer.Return();
    
    [Test]
    public void Fresh_buffer_write_position_should_be_zero() =>
        Assert.That(_buffer.Size, Is.EqualTo(0));

    [Test]
    public void Reading_value_that_was_written_produces_same_value<T>([ValueSource(typeof(TestData), nameof(TestData.Values))] T valueToWrite, [Values(0, 1)] int offset)
        where T : unmanaged
    {
        _buffer.Write(valueToWrite, offset);
        var value = _buffer.Read<T>(offset);
        Assert.That(value, Is.EqualTo(valueToWrite));
    }

    [Test]
    public void Reading_array_that_was_written_produces_same_array<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] arrayToWrite, [Values(0, 1)] int offset)
        where T : unmanaged
    {
        _buffer.WriteArray(arrayToWrite, offset);
        var array = _buffer.ReadArray<T>(arrayToWrite.Length, offset);
        Assert.That(array, Is.EqualTo(arrayToWrite));
    }
    
    [Test]
    public void Returning_buffer_out_of_pool_successfully_returns()
    {
        var buffer = Buffer.Get();
        Assert.That(buffer.Return(), Is.True);
    }

    [Test]
    public void Returning_buffer_in_pool_does_not_return()
    {
        var buffer = Buffer.Get();
        buffer.Return();
        Assert.That(buffer.Return(), Is.False);
    }
    
    [Test]
    public void Returning_buffer_should_return_big_buffer_to_array_pool()
    {
        var buffer = Buffer.Get();
        buffer.WriteArray(new byte[Packet.BufferSize + 1]);
        buffer.Return();
        
        Assert.That(buffer.Capacity, Is.Not.GreaterThan(Packet.BufferSize));
    }
    
    [Test]
    public void Getting_byte_array_from_buffer_in_pool_throws()
    {
        var buffer = Buffer.Get();
        buffer.Return();
        Assert.That(() => buffer.Bytes, Throws.Exception);
    }
    
    [Test]
    public void Writing_to_buffer_in_pool_throws()
    {
        var buffer = Buffer.Get();
        buffer.Return();
        
        Assert.That(() => buffer.Write(0), Throws.Exception);
        Assert.That(() => buffer.WriteArray(new[] { 1, 2, 3 }), Throws.Exception);
    }
    
    [Test]
    public void Writing_out_of_bounds_should_increase_buffer_size()
    {
        var buffer = Buffer.Get();
        buffer.WriteArray(new byte[Packet.BufferSize + 1]);
        Assert.That(buffer.Capacity, Is.GreaterThan(Packet.BufferSize));
    }

    [Test]
    public void Reading_from_buffer_in_pool_throws()
    {
        var buffer = Buffer.Get();
        buffer.Return();
        
        Assert.That(() => buffer.Read<int>(offset: 0), Throws.Exception);
        Assert.That(() => buffer.ReadArray<int>(length: 0, offset: 0), Throws.Exception);
    }

    [Test]
    public void Reading_value_that_was_written_produces_same_value<T>([ValueSource(typeof(TestData), nameof(TestData.Values))] T valueToWrite) where T : unmanaged
    {
        _buffer.Write(valueToWrite);
        var value = _buffer.Read<T>(offset: 0);
        Assert.That(value, Is.EqualTo(valueToWrite));
    }

    [Test]
    public void Reading_array_that_was_written_produces_same_array<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] arrayToWrite) where T : unmanaged
    {
        _buffer.WriteArray(arrayToWrite);
        var array = _buffer.ReadArray<T>(arrayToWrite.Length, offset: 0);
        Assert.That(array, Is.EqualTo(arrayToWrite));
    }
    
    [Test]
    public void Reading_var_int_that_was_written_produces_same_var_int([ValueSource(typeof(TestData), nameof(TestData.VarInts))] int varIntToWrite, [Values(0, 1)] int offset)
    {
        _buffer.WriteVarInt(varIntToWrite, offset);
        var varInt = _buffer.ReadVarInt(offset, out _);
        Assert.That(varInt, Is.EqualTo(varIntToWrite));
    }

    [TestCase(0, ExpectedResult = 1)]
    [TestCase(127, ExpectedResult = 1)]
    [TestCase(128, ExpectedResult = 2)]
    [TestCase(16_383, ExpectedResult = 2)]
    [TestCase(16_384, ExpectedResult = 3)]
    [TestCase(2_097_151, ExpectedResult = 3)]
    [TestCase(2_097_152, ExpectedResult = 4)]
    [TestCase(268_435_455, ExpectedResult = 4)]
    [TestCase(268_435_456, ExpectedResult = 5)]
    [TestCase(int.MaxValue, ExpectedResult = 5)]
    [TestCase(int.MinValue, ExpectedResult = 5)]
    [TestCase(-1, ExpectedResult = 5)]
    public int Var_int_bytes_needed_returns_proper_number_of_bytes(int value) => Buffer.VarIntBytesNeeded(value);
    
    [TestCase(0, 1)]
    [TestCase(127, 1)]
    [TestCase(128, 2)]
    [TestCase(16_383, 2)]
    [TestCase(16_384, 3)]
    [TestCase(2_097_151, 3)]
    [TestCase(2_097_152, 4)]
    [TestCase(268_435_455, 4)]
    [TestCase(268_435_456, 5)]
    [TestCase(int.MaxValue, 5)]
    [TestCase(int.MinValue, 5)]
    [TestCase(-1, 5)]
    public void Writing_and_reading_var_int_uses_correct_number_of_bytes(int value, int bytesNeeded)
    {
        _buffer.WriteVarInt(value);
        Assert.That(_buffer.Size, Is.EqualTo(bytesNeeded));

        _buffer.ReadVarInt(offset: 0, out var bytesRead);
        Assert.That(bytesRead, Is.EqualTo(bytesNeeded));
    }
    
    [Test]
    public void Reading_var_int_that_uses_more_than_5_bytes_throws()
    {
        for (var i = 0; i < 6; i++)
            _buffer.Bytes[i] = 0x80;

        Assert.That(() => _buffer.ReadVarInt(offset: 0, out _), Throws.Exception);
    }
    
    [Test]
    public void Reading_var_int_that_uses_out_of_bounds_bytes_throws()
    {
        _buffer.Bytes[^1] = 0x80;
        Assert.That(() => _buffer.ReadVarInt(offset: _buffer.Bytes.Length - 1, out _), Throws.Exception);
        Assert.That(() => _buffer.ReadVarInt(offset: _buffer.Bytes.Length, out _), Throws.Exception);
    }

    [Test]
    public void Buffer_copy_returns_functionally_equal_buffer()
    {
        _buffer.WriteArray(new byte[] { 0x00, 0x01, 0x02 });
        var buffer = Buffer.Copy(_buffer);
        
        Assert.That(buffer.Size, Is.EqualTo(_buffer.Size));
        Assert.That(buffer.Bytes[0], Is.EqualTo(_buffer.Bytes[0]));
        Assert.That(buffer.Bytes[1], Is.EqualTo(_buffer.Bytes[1]));
        Assert.That(buffer.Bytes[2], Is.EqualTo(_buffer.Bytes[2]));
    }
    
    [Test]
    public void Buffer_from_returns_buffer_that_has_provided_byte_array()
    {
        var bytes = new byte[] { 0x00, 0x01, 0x02 };
        var buffer = Buffer.From(bytes, bytes.Length);
        
        Assert.That(buffer.Size, Is.EqualTo(bytes.Length));
        Assert.That(buffer.Bytes[0], Is.EqualTo(bytes[0]));
        Assert.That(buffer.Bytes[1], Is.EqualTo(bytes[1]));
        Assert.That(buffer.Bytes[2], Is.EqualTo(bytes[2]));
    }
    
    [Test]
    public void Buffer_of_size_returns_buffer_of_at_least_specified_size([Values(1, 10, 100, 1000, 10_000, 100_000)] int size)
    {
        var buffer = Buffer.OfSize(size);
        Assert.That(buffer.Capacity, Is.GreaterThanOrEqualTo(size));
    }
}
