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
}
