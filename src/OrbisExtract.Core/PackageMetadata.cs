namespace OrbisExtract.Core;

public sealed record PackageMetadata(string Title, string Version, string ContentId);

public interface IPkgMetadataReader
{
    Task<PackageMetadata> ReadAsync(Stream package, CancellationToken cancellationToken = default);
}
