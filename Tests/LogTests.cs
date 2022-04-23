using NUnit.Framework;

namespace Link.Tests;

[TestFixture]
public class LogTests
{
    [Test]
    public void Default_loggers_should_not_be_null()
    {
        Assert.That(() => Log.Info(string.Empty), Throws.Nothing);
        Assert.That(() => Log.Warning(string.Empty), Throws.Nothing);
        Assert.That(() => Log.Error(string.Empty), Throws.Nothing);
    }
    
    [Test]
    public void Loggers_cannot_be_set_to_null()
    {
        Assert.That(() => Log.Info = null, Throws.Exception);
        Assert.That(() => Log.Warning = null, Throws.Exception);
        Assert.That(() => Log.Error = null, Throws.Exception);
    }
}
