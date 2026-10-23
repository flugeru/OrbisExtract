using OrbisExtract.Core;

namespace OrbisExtract.Tests;

public sealed class PathSecurityTests
{
    [Fact]
    public void RejectsTraversal()
    {
        Assert.Throws<PkgExtractionException>(() => SafeExtractionPath.Resolve("C:\\out", "../escape.txt"));
    }

    [Fact]
    public void KeepsNestedPathInsideDestination()
    {
        var path = SafeExtractionPath.Resolve("C:\\out", "nested/file.bin");
        Assert.StartsWith(Path.GetFullPath("C:\\out"), path, StringComparison.OrdinalIgnoreCase);
    }
}
