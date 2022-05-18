using NUnit.Framework;

namespace Link.Tests.Unit;

[TestFixture]
public class ArrayPoolTests
{
    [Test]
    public void Getting_array_with_negative_size_throws() =>
        Assert.That(() => ArrayPool.Get(-1), Throws.Exception);
    
    [Test]
    public void Getting_array_with_max_size_does_not_throw() =>
        Assert.That(() => ArrayPool.Get(ArrayPool.MaxSize), Throws.Nothing);
    
    [Test]
    public void Getting_array_with_size_greater_than_max_throws() =>
        Assert.That(() => ArrayPool.Get(ArrayPool.MaxSize + 1), Throws.Exception);
    
    [Test]
    public void Getting_array_with_size_0_returns_same_instance()
    {
        var emptyArray1 = ArrayPool.Get(0);
        var emptyArray2 = ArrayPool.Get(0);
        Assert.That(emptyArray1 == emptyArray2);
    }
    
    [Test]
    public void Returning_null_array_throws() =>
        Assert.That(() => ArrayPool.Return(null), Throws.Exception);
    
    [Test]
    public void Returning_array_with_max_size_does_not_throw() =>
        Assert.That(() => ArrayPool.Return(new byte[ArrayPool.MaxSize]), Throws.Nothing);
    
    [Test]
    public void Returning_array_with_size_greater_than_max_throws() =>
        Assert.That(() => ArrayPool.Return(new byte[ArrayPool.MaxSize + 1]), Throws.Exception);
    
    [Test]
    public void Getting_array_always_returns_multiple_of_packet_buffer_size([Values(1, 10, 100, 1000, 10_000, 100_000)] int size)
    {
        var array = ArrayPool.Get(size);
        Assert.That(array.Length % Packet.BufferSize == 0);
    }
    
    [Test]
    public void Getting_array_always_returns_array_with_enough_capacity([Values(1, 10, 100, 1000, 10_000, 100_000)] int size)
    {
        var array = ArrayPool.Get(size);
        Assert.That(array.Length >= size);
    }

    [Test]
    public void Calculating_bucket_index()
    {
        var powerOfTwo = 1;
        
        for (var i = 0; i <= 8; i++)
        {
            Assert.That(ArrayPool.CalculateBucketIndex(minimalLength: Packet.BufferSize * (powerOfTwo / 2) + 1), Is.EqualTo(i));
            Assert.That(ArrayPool.CalculateBucketIndex(minimalLength: Packet.BufferSize * powerOfTwo), Is.EqualTo(i));
            
            powerOfTwo *= 2;
        }
    }
}
