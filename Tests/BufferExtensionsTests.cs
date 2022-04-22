using System;
using NUnit.Framework;

namespace Link.Tests;

[TestFixture]
public class BufferExtensionsTests
{
    private static readonly object[] Values =
    {
        sbyte.MaxValue,
        byte.MaxValue,
        short.MaxValue,
        ushort.MaxValue,
        int.MaxValue,
        uint.MaxValue,
        long.MaxValue,
        ulong.MaxValue,
        float.MaxValue,
        double.MaxValue,
        char.MaxValue,
        true,
        false, 
    };
    
    private static readonly object[] Arrays =
    {
        new sbyte[] { 1, 2, 3 },
        new byte[] { 1, 2, 3 },
        new short[] { 1, 2, 3 },
        new ushort[] { 1, 2, 3 },
        new[] { 1, 2, 3 },
        new uint[] { 1, 2, 3 },
        new long[] { 1, 2, 3 },
        new ulong[] { 1, 2, 3 },
        new float[] { 1, 2, 3 },
        new double[] { 1, 2, 3 },
        new[] { true, false, true },
        new[] { 'A', 'b', 'C' },
    };

    private static readonly int[] VarInts =
    {
        0,
        127,
        128,
        16_383,
        16_384,
        2_097_151,
        2_097_152,
        268_435_455,
        268_435_456,
        int.MaxValue,
        int.MinValue,
        -1,
    };
    
    private readonly byte[] _buffer = new byte[1024];

    [SetUp]
    public void Clear_buffer() => Array.Clear(_buffer);

    [Test]
    public void Reading_value_that_was_written_produces_same_value<T>([ValueSource(nameof(Values))] T valueToWrite, [Values(0, 1)] int offset) where T : unmanaged
    {
        _buffer.Write(valueToWrite, offset);
        var value = _buffer.Read<T>(offset);
        Assert.That(value, Is.EqualTo(valueToWrite));
    }

    [Test]
    public void Reading_array_that_was_written_produces_same_array<T>([ValueSource(nameof(Arrays))] T[] arrayToWrite, [Values(0, 1)] int offset) where T : unmanaged
    {
        _buffer.WriteArray(arrayToWrite, offset);
        var array = _buffer.ReadArray<T>(offset);
        Assert.That(array, Is.EqualTo(arrayToWrite));
    }
    
    [Test]
    public void Reading_slice_that_was_written_produces_same_slice<T>([ValueSource(nameof(Arrays))] T[] sliceToWrite, [Values(0, 1)] int offset) where T : unmanaged
    {
        _buffer.WriteSlice(sliceToWrite, start: 0, length: sliceToWrite.Length, offset);
        var slice = _buffer.ReadSlice<T>(sliceToWrite.Length, offset);
        Assert.That(slice, Is.EqualTo(sliceToWrite));
    }

    [Test]
    public void Reading_var_int_that_was_written_produces_same_var_int([ValueSource(nameof(VarInts))] int varIntToWrite, [Values(0, 1)] int offset)
    {
        _buffer.WriteVarInt(varIntToWrite, offset);
        var varInt = _buffer.ReadVarInt(offset);
        Assert.That(varInt, Is.EqualTo(varIntToWrite));
    }
}
