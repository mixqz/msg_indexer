using EmailIndexer.Core;
using Xunit;

public class SmokeTests
{
    [Fact]
    public void AppInfo_HasVersion() => Assert.False(string.IsNullOrEmpty(AppInfo.Version));
}
