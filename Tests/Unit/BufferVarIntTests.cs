using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class BufferVarIntTests
{
    private Buffer _buffer;

    [SetUp]
    public void Get_buffer() => _buffer = Buffer.Get();

    [TearDown]
    public void Return_buffer() => _buffer.Return();

    [Test]
    public void Reading_var_int_that_was_written_produces_same_var_int([ValueSource(typeof(TestData), nameof(TestData.VarInts))] int varIntToWrite, [Values(0, 1)] int offset)
    {
        _buffer.WriteVarInt(varIntToWrite, offset);
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
        _buffer.WriteVarInt(value);
        Assert.That(_buffer.Size, Is.EqualTo(bytesNeeded));

        _buffer.ReadVarInt(offset: 0, out var bytesRead);
        Assert.That(bytesRead, Is.EqualTo(bytesNeeded));
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
}
