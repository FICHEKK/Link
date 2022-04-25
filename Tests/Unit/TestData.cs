using System.Collections.Generic;

namespace Link.Tests.Unit;

public static class TestData
{
    public static readonly IEnumerable<object> Values = new object[]
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
    
    public static readonly IEnumerable<object> Arrays = new object[]
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

    public static readonly IEnumerable<int> VarInts = new[]
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
}
