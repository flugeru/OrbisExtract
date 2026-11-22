using LibOrbisPkg.PKG;
using OrbisExtract.Core;
using System.Text.RegularExpressions;
using System.Buffers.Binary;

namespace OrbisExtract.Infrastructure.OpenOrbis;

public sealed record PackagePreview(PackageMetadata Metadata, byte[]? Artwork, int ArtworkWidth);

public sealed class OpenOrbisPkgMetadataReader : IPkgMetadataReader
{
    private const int MaxPrefix = 16 * 1024 * 1024;
    private static readonly Regex TitleIdPattern = new(@"\A[A-Z]{4}[0-9]{5}\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public async Task<PackageMetadata> ReadAsync(Stream package, CancellationToken cancellationToken = default) =>
        (await ReadPackageAsync(package, false, cancellationToken).ConfigureAwait(false)).Metadata;

    public Task<PackagePreview> ReadPreviewAsync(Stream package, CancellationToken cancellationToken = default) =>
        ReadPackageAsync(package, true, cancellationToken);

    private static async Task<PackagePreview> ReadPackageAsync(Stream package, bool includeArtwork, CancellationToken cancellationToken)
    {
        if (package.CanSeek)
            return await Task.Run(() => Parse(package, includeArtwork, false, cancellationToken), cancellationToken).ConfigureAwait(false);

        // Common PKGs keep metadata near the header. A non-seekable provider
        // can identify them without staging the entire payload.
        using var prefix = new MemoryStream(1024 * 1024);
        var buffer = new byte[64 * 1024];
        for (int limit = 1024 * 1024; limit <= MaxPrefix; limit *= 2)
        {
            prefix.Position = prefix.Length;
            while (prefix.Length < limit)
            {
                var count = await package.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, limit - prefix.Length)), cancellationToken).ConfigureAwait(false);
                if (count == 0) break;
                prefix.Write(buffer, 0, count);
            }
            try
            {
                return Parse(prefix, includeArtwork, true, cancellationToken, prefix.Length == limit && limit < MaxPrefix);
            }
            catch (InvalidDataException) when (prefix.Length == limit && limit < MaxPrefix)
            {
                // A larger metadata area may still fit within the bounded probe.
            }
        }
        throw new PkgExtractionException("Os metadados do PKG excedem o limite de leitura do provedor.");
    }

    private static PackagePreview Parse(Stream package, bool includeArtwork, bool prefix, CancellationToken token, bool canReadMore = false)
    {
        var pkg = new PkgReader(package).ReadMetadata(token, metadataPrefix: prefix);
        var metadata = Describe(pkg);
        if (!includeArtwork) return new(metadata, null, 0);

        // icon0 is the square console tile. pic1 supplies a wider alternative.
        Span<byte> header = stackalloc byte[24];
        foreach (var id in new[] { EntryId.ICON0_PNG, EntryId.PIC1_PNG })
        {
            token.ThrowIfCancellationRequested();
            var entry = pkg.Metas.Metas.FirstOrDefault(entry => entry.id == id && !entry.Encrypted);
            if (entry is null || entry.DataSize < header.Length || entry.DataSize > 6 * 1024 * 1024)
                continue;
            ulong end = (ulong)entry.DataOffset + entry.DataSize;
            if (end > (ulong)package.Length)
            {
                if (canReadMore && end <= MaxPrefix) throw new InvalidDataException("Artwork outside the readable prefix");
                continue;
            }

            package.Position = entry.DataOffset;
            package.ReadExactly(header);
            if (BinaryPrimitives.ReadUInt64BigEndian(header) != 0x89504E470D0A1A0AUL ||
                BinaryPrimitives.ReadUInt32BigEndian(header[8..]) != 13 || !header[12..16].SequenceEqual("IHDR"u8))
                continue;

            int width = BinaryPrimitives.ReadInt32BigEndian(header[16..]);
            int height = BinaryPrimitives.ReadInt32BigEndian(header[20..]);
            if (width <= 0 || height <= 0 || width > 4096 || height > 4096 || (long)width * height > 8_388_608)
                continue;

            var artwork = new byte[entry.DataSize];
            header.CopyTo(artwork);
            package.ReadExactly(artwork.AsSpan(header.Length));
            return new(metadata, artwork, width);
        }
        return new(metadata, null, 0);
    }

    private static PackageMetadata Describe(Pkg pkg)
    {
        string? ReadValue(string key)
        {
            var value = pkg.ParamSfo?.ParamSfo?[key];
            return value?.ToString()?.TrimEnd('\0').Trim();
        }

        var contentId = pkg.Header.content_id ?? "";
        var titleId = ReadValue("TITLE_ID");
        var headerTitle = contentId.Split('-', 3).ElementAtOrDefault(1)?.Split('_', 2)[0];
        if (headerTitle is null || !TitleIdPattern.IsMatch(headerTitle)) headerTitle = null;
        if (titleId is null || !TitleIdPattern.IsMatch(titleId)) titleId = null;
        if (titleId is not null && headerTitle is not null && !titleId.Equals(headerTitle, StringComparison.OrdinalIgnoreCase))
            throw new PkgExtractionException("TITLE_ID e Content ID são incompatíveis no PKG.");

        var category = ReadValue("CATEGORY")?.ToLowerInvariant();
        var flags = pkg.Header.content_flags;
        var kind = pkg.Header.content_type is ContentType.GD or ContentType.DP
            ? flags.HasFlag(ContentFlags.DELTA_PATCH) ? PackageKind.DeltaUpdate
            : pkg.Header.content_type == ContentType.DP || category == "gp" ||
              (flags & (ContentFlags.FIRST_PATCH | ContentFlags.SUBSEQUENT_PATCH)) != 0
                ? PackageKind.Update : PackageKind.BaseGame
            : PackageKind.Other;
        var appVersion = ReadValue("APP_VER");
        Version.TryParse(appVersion, out var parsedVersion);
        const long MiB = 1024 * 1024;
        // Larger images can carry larger sector maps and inode indexes. The
        // estimate includes room for the stream and native crypto buffers.
        long estimatedMemory = 48 * MiB + (long)Math.Min(pkg.Header.pfs_image_size / 512, 336UL * (ulong)MiB);
        return new PackageMetadata(
            ReadValue("TITLE") ?? "Título não informado",
            appVersion ?? ReadValue("VERSION") ?? "Versão não informada",
            contentId)
        {
            TitleId = (titleId ?? headerTitle)?.ToUpperInvariant(),
            ApplicationVersion = parsedVersion,
            Kind = kind,
            EstimatedWorkingSetBytes = estimatedMemory
        };
    }
}
