using System;
using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class BufferExtensionsTests
{
    private readonly byte[] _buffer = new byte[1024];

    [SetUp]
    public void Clear_buffer() => Array.Clear(_buffer);

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
        var array = _buffer.ReadArray<T>(offset);
        Assert.That(array, Is.EqualTo(arrayToWrite));
    }
    
    [Test]
    public void Reading_slice_that_was_written_produces_same_slice<T>([ValueSource(typeof(TestData), nameof(TestData.Arrays))] T[] sliceToWrite, [Values(0, 1)] int offset)
        where T : unmanaged
    {
        _buffer.WriteSlice(sliceToWrite, start: 0, length: sliceToWrite.Length, offset);
        var slice = _buffer.ReadSlice<T>(sliceToWrite.Length, offset);
        Assert.That(slice, Is.EqualTo(sliceToWrite));
    }

    [Test]
    public void Reading_var_int_that_was_written_produces_same_var_int([ValueSource(typeof(TestData), nameof(TestData.VarInts))] int varIntToWrite, [Values(0, 1)] int offset)
    {
        _buffer.WriteVarInt(varIntToWrite, offset, out _);
        var varInt = _buffer.ReadVarInt(offset, out _);
        Assert.That(varInt, Is.EqualTo(varIntToWrite));
    }

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
        _buffer.WriteVarInt(value, offset: 0, out var bytesWritten);
        Assert.That(bytesWritten, Is.EqualTo(bytesNeeded));

        _buffer.ReadVarInt(offset: 0, out var bytesRead);
        Assert.That(bytesRead, Is.EqualTo(bytesNeeded));
    }

    [Test]
    public void Reading_var_int_that_uses_more_than_5_bytes_throws()
    {
        var invalidVarIntEncoding = new byte[6];

        for (var i = 0; i < invalidVarIntEncoding.Length; i++)
            invalidVarIntEncoding[i] = 0x80;

        Assert.That(() => invalidVarIntEncoding.ReadVarInt(offset: 0, out _), Throws.Exception);
    }
}
