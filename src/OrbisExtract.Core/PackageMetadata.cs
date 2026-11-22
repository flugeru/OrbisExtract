namespace OrbisExtract.Core;

public enum PackageKind { Other, BaseGame, Update, DeltaUpdate }

public sealed record PackageMetadata(string Title, string Version, string ContentId)
{
    public string? TitleId { get; init; }
    public Version? ApplicationVersion { get; init; }
    public PackageKind Kind { get; init; }
    public long EstimatedWorkingSetBytes { get; init; }
}

public interface IPkgMetadataReader
{
    Task<PackageMetadata> ReadAsync(Stream package, CancellationToken cancellationToken = default);
}
