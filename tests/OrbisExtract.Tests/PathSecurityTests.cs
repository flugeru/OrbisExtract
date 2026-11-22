using OrbisExtract.Core;

namespace OrbisExtract.Tests;

public sealed class PathSecurityTests
{
    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("/absolute")]
    [InlineData("C:drive-relative")]
    [InlineData("file.txt:stream")]
    [InlineData("nested//file.txt")]
    [InlineData("nested/./file.txt")]
    public void RejectsUnsafePath(string path)
    {
        Assert.Throws<PkgExtractionException>(() => SafeExtractionPath.Resolve(Path.GetTempPath(), path));
    }

    [Fact]
    public void KeepsNestedPathInsideDestination()
    {
        var root = Path.GetTempPath();
        var path = SafeExtractionPath.Resolve(root, "nested/file.bin");
        Assert.Equal(Path.Combine(root, "nested", "file.bin"), path);
    }
}
