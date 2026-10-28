using LibOrbisPkg.PFS;
using LibOrbisPkg.PKG;
using LibOrbisPkg.Util;
using OrbisExtract.Core;

namespace OrbisExtract.Infrastructure.OpenOrbis;

public sealed class OpenOrbisPkgExtractor : IPkgExtractor
{
    public async Task<ExtractionResult> ExtractAsync(
        ExtractionRequest request,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request.Package);
        if (request.Destination is null)
            Directory.CreateDirectory(request.DestinationDirectory);
        if (!request.Package.CanSeek)
            throw new PkgExtractionException("A biblioteca LibOrbisPkg exige uma origem pesquisável.");

        var package = await Task.Run(() => ReadPackage(request.Package), cancellationToken).ConfigureAwait(false);
        // Fake PKGs commonly use the all-zero passcode. Verify it against the
        // package digest rather than assuming it applies to every PKG.
        var passcode = request.Passcode;
        if (string.IsNullOrEmpty(passcode) && package.CheckPasscode(new string('0', 32)))
            passcode = new string('0', 32);
        var passcodeValid = !string.IsNullOrEmpty(passcode) && package.CheckPasscode(passcode);
        if (!string.IsNullOrEmpty(request.Passcode) && !passcodeValid)
            throw new PkgExtractionException("Passcode inválido para este PKG.");
        var ekpfs = passcodeValid
            ? Crypto.ComputeKeys(package.Header.content_id, passcode!, 1)
            : package.GetEkpfs();
        if (ekpfs is null)
            throw new PkgExtractionException("Este PKG é criptografado ou exige passcode.");

        cancellationToken.ThrowIfCancellationRequested();
        request.Package.Position = 0;
        using var accessor = new PkgMemoryReader(request.Package, (long)package.Header.pfs_image_offset, (long)package.Header.pfs_image_size);
        var outer = new PfsReader(accessor, package.Header.pfs_flags, ekpfs);
        var inner = new PfsReader(new PFSCReader(outer.GetFile("pfs_image.dat").GetView()));
        // The game payload is in the inner PFS. Some packages also carry Sc0
        // files in the outer PFS; most system assets are PKG table entries.
        var files = new List<(string RelativePath, long Size, Func<Stream> Open)>();
        foreach (var file in inner.GetAllFiles())
        {
            var path = NormalizePkgToolPath(file.FullName);
            path = RemoveContainerRoot(path, "uroot");
            path = RemoveContainerRoot(path, "Image0");
            files.Add((path, file.size, () => new PfsFileStream(file)));
        }

        foreach (var file in outer.GetAllFiles())
        {
            var path = NormalizePkgToolPath(file.FullName);
            if (path.Equals("pfs_image.dat", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("/pfs_image.dat", StringComparison.OrdinalIgnoreCase))
                continue;

            // Only Sc0 is an extractable system tree in the outer PFS. The
            // remaining outer entries are PKG containers/index data and must not
            // be emitted as game files (especially pfs_image.dat).
            path = RemoveContainerRoot(path, "uroot");
            if (path.StartsWith("Sc0/", StringComparison.OrdinalIgnoreCase))
                path = "sce_sys/" + path["Sc0/".Length..];
            else if (path.StartsWith("sce_sys/", StringComparison.OrdinalIgnoreCase))
            {
                // Already in the final system-tree layout.
            }
            else
            {
                continue;
            }

            path = NormalizePkgToolPath(path);
            files.Add((path, file.size, () => new PfsFileStream(file)));
        }

        // sce_sys assets in a full PKG are *package table entries*, not files
        // inside either PFS image. This is how Gp4Creator in PkgTool extracts
        // param.sfo, icons, licenses, PlayGo data and trophies. Copy the raw
        // bytes at the entry's DataOffset (decrypting encrypted entries);
        // parsing/re-serializing param.sfo would change the dump.
        foreach (var entry in package.Metas.Metas)
        {
            if (!EntryNames.IdToName.TryGetValue(entry.id, out var name) ||
                name.StartsWith(".", StringComparison.Ordinal))
                continue;

            var path = NormalizePkgToolPath("sce_sys/" + name);
            var readSize = entry.Encrypted ? ((long)entry.DataSize + 15) & ~15L : entry.DataSize;
            if ((ulong)entry.DataOffset + (ulong)readSize > (ulong)request.Package.Length)
                throw new PkgExtractionException($"Entrada PKG fora dos limites: {path}");
            files.Add((path, entry.DataSize,
                () => OpenPkgEntry(request.Package, package, entry, passcode, readSize)));
        }

        files = files
            .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var duplicate = files.GroupBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new PkgExtractionException($"Caminho duplicado no PKG: {duplicate.Key}");
        long count = 0, bytes = 0;
        var totalBytes = files.Sum(item => item.Size);
        var buffer = new byte[1024 * 1024];
        foreach (var item in files)
        {
            if (request.PauseGate is not null) await request.PauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var destination = request.Destination ?? new LocalExtractionDestination(request.DestinationDirectory, request.FailOnExistingFiles);
            Stream? target = null;
            try
            {
                var relativePath = item.RelativePath;
                if (string.IsNullOrWhiteSpace(relativePath))
                    throw new PkgExtractionException("O pacote contém um caminho interno vazio.");

                target = await destination.CreateTemporaryFileAsync(relativePath, cancellationToken).ConfigureAwait(false);
                using var source = item.Open();
                long fileBytes = 0;
                var lastReport = System.Diagnostics.Stopwatch.StartNew();
                progress?.Report(new ExtractionProgress("Extraindo", relativePath, count, files.Count, bytes, totalBytes));
                while (true)
                {
                    if (request.PauseGate is not null) await request.PauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    fileBytes += read;
                    if (lastReport.ElapsedMilliseconds >= 150 || fileBytes == item.Size)
                    {
                        progress?.Report(new ExtractionProgress("Extraindo", relativePath, count, files.Count, bytes + fileBytes, totalBytes));
                        lastReport.Restart();
                    }
                }

                if (fileBytes != item.Size)
                    throw new PkgExtractionException($"Arquivo truncado: {relativePath} ({fileBytes}/{item.Size} bytes).");

                await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                await destination.CommitAsync(target, relativePath, cancellationToken).ConfigureAwait(false);
                target = null;
                count++;
                bytes += fileBytes;
                progress?.Report(new ExtractionProgress("Extraindo", relativePath, count, files.Count, bytes, totalBytes));
            }
            catch
            {
                if (target is not null)
                {
                    // Cleanup must never replace the real extraction error or cancellation
                    // with a secondary SAF DeleteAsync failure.
                    try
                    {
                        await destination.DeleteTemporaryFileAsync(target, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                        // The original exception is more useful; some Android document providers
                        // refuse deletion even after a write was cancelled.
                    }
                }
                throw;
            }
        }
        progress?.Report(new ExtractionProgress("Concluído", null, count, files.Count, bytes, null));
        return new ExtractionResult(count, bytes);
    }

    private static string NormalizePfsPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Split('/').Any(segment => segment is "." or ".."))
            throw new PkgExtractionException($"Caminho interno inseguro no PKG: {path}");
        return normalized;
    }

    private static string NormalizePkgToolPath(string path)
    {
        var normalized = NormalizePfsPath(path);
        if (normalized.Length == 0)
            throw new PkgExtractionException("O PKG contém uma entrada sem nome.");
        return normalized.TrimStart('/');
    }

    private static string RemoveContainerRoot(string path, string root)
    {
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        var prefix = root + "/";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? path[prefix.Length..]
            : path;
    }

    private static Stream OpenPkgEntry(Stream stream, Pkg package, MetaEntry entry, string? passcode, long readSize)
    {
        if (!entry.Encrypted)
            return new PkgStreamAccessor(stream, entry.DataOffset, entry.DataSize);
        if (entry.KeyIndex != 3 && string.IsNullOrEmpty(passcode))
            throw new PkgExtractionException($"A entrada {entry.id} exige passcode para descriptografia.");
        if (readSize > int.MaxValue)
            throw new PkgExtractionException($"Entrada criptografada grande demais: {entry.id}");
        var encrypted = new byte[(int)readSize];
        using (var source = new PkgStreamAccessor(stream, entry.DataOffset, readSize))
            source.ReadExactly(encrypted);
        var decrypted = entry.KeyIndex == 3
            ? Entry.Decrypt(encrypted, package, entry)
            : Entry.Decrypt(encrypted, package.Header.content_id, passcode!, entry);
        return new MemoryStream(decrypted, 0, checked((int)entry.DataSize), writable: false);
    }

    private static Pkg ReadPackage(Stream stream)
    {
        try { return new PkgReader(stream).ReadPkg(); }
        catch (Exception ex) { throw new PkgExtractionException("O arquivo não é um PKG válido ou está corrompido.", ex); }
    }

    private sealed class PfsFileStream : Stream
    {
        private readonly PfsReader.File file; private long position;
        public PfsFileStream(PfsReader.File file) => this.file = file;
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Length => file.size; public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { var allowed = (int)Math.Min(count, Length - position); if (allowed <= 0) return 0; file.GetView().Read(position, buffer, offset, allowed); position += allowed; return allowed; }
        public override long Seek(long offset, SeekOrigin origin) { var next = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => position + offset, SeekOrigin.End => Length + offset, _ => throw new ArgumentOutOfRangeException(nameof(origin)) }; if (next < 0 || next > Length) throw new IOException("Seek fora dos limites do arquivo PFS."); position = next; return position; }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class PkgMemoryReader : IMemoryReader
    {
        private readonly Stream stream; private readonly long start, length;
        public PkgMemoryReader(Stream stream, long start, long length) { this.stream = stream; this.start = start; this.length = length; }
        public void Read(long position, byte[] buffer, int offset, int count)
        {
            if (position < 0 || count < 0 || position > length - count) throw new EndOfStreamException("Leitura fora dos limites do PKG.");
            stream.Position = start + position;
            var read = 0;
            while (read < count)
            {
                var n = stream.Read(buffer, offset + read, count - read);
                if (n == 0) throw new EndOfStreamException("PKG truncado.");
                read += n;
            }
        }
        public void Dispose() { }
    }

    private sealed class PkgStreamAccessor : Stream
    {
        private readonly Stream inner; private readonly long start, length; private long position;
        public PkgStreamAccessor(Stream inner, long start, long length) { this.inner = inner; this.start = start; this.length = length; }
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Length => length; public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { var allowed = (int)Math.Min(count, length - position); if (allowed <= 0) return 0; inner.Position = start + position; var n = inner.Read(buffer, offset, allowed); position += n; return n; }
        public override long Seek(long offset, SeekOrigin origin) { var next = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => position + offset, SeekOrigin.End => length + offset, _ => throw new ArgumentOutOfRangeException(nameof(origin)) }; if (next < 0 || next > length) throw new IOException("Seek fora dos limites do PFS."); position = next; return position; }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
