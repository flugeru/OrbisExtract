using LibOrbisPkg.PFS;
using LibOrbisPkg.PKG;
using LibOrbisPkg.Util;
using OrbisExtract.Core;
using System.Diagnostics;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace OrbisExtract.Infrastructure.OpenOrbis;

public sealed class OpenOrbisPkgExtractor : IPkgExtractor
{
    private const int CopyBlockSize = 1024 * 1024;
    private const int ReadAheadMinimum = 8 * CopyBlockSize;

    public async Task<ExtractionResult> ExtractAsync(
        ExtractionRequest request,
        IProgress<ExtractionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request.Package);
        var preparationStarted = Stopwatch.GetTimestamp();
        if (!request.Package.CanRead || !request.Package.CanSeek)
            throw new PkgExtractionException("A biblioteca LibOrbisPkg exige uma origem pesquisável.");

        var package = await Task.Run(() => ReadPackage(request.Package, cancellationToken), cancellationToken).ConfigureAwait(false);
        var packageLength = (ulong)request.Package.Length;
        if (package.Header.pfs_image_offset > packageLength ||
            package.Header.pfs_image_size > packageLength - package.Header.pfs_image_offset ||
            package.Header.pfs_image_size == 0)
            throw new PkgExtractionException("Imagem PFS ausente ou fora dos limites do PKG.");
        // Fake PKGs commonly use the all-zero passcode. Verify it against the
        // package digest rather than assuming it applies to every PKG.
        var passcode = request.Passcode;
        passcode = string.IsNullOrEmpty(passcode) ? new string('0', 32) : passcode;
        var passcodeValid = package.EntryKeys is not null && package.CheckPasscode(passcode);
        if (!string.IsNullOrEmpty(request.Passcode) && !passcodeValid)
            throw new PkgExtractionException("Passcode inválido para este PKG.");
        if (!passcodeValid) passcode = null;
        var ekpfs = passcodeValid
            ? Crypto.ComputeKeys(package.Header.content_id, passcode!, 1)
            : package.GetEkpfs();
        if (ekpfs is null)
            throw new PkgExtractionException("Este PKG é criptografado ou exige passcode.");

        cancellationToken.ThrowIfCancellationRequested();
        request.Package.Position = 0;
        var timings = new ReadTimings();
        var imageLength = (long)package.Header.pfs_image_size;
        using var accessor = new PkgMemoryReader(request.Package, (long)package.Header.pfs_image_offset, imageLength, timings, cancellationToken);
        var outer = new PfsReader(accessor, package.Header.pfs_flags, ekpfs, timings: timings, imageLength: imageLength);
        var innerImage = outer.GetFile("pfs_image.dat") ??
            throw new PkgExtractionException("O PKG não contém a imagem interna pfs_image.dat.");
        using var pfsc = new PFSCReader(innerImage.GetView(), innerImage.size) { Timings = timings };
        var inner = new PfsReader(pfsc, timings: timings, imageLength: pfsc.Length);
        // The game payload is in the inner PFS. Some packages also carry Sc0
        // files in the outer PFS; most system assets are PKG table entries.
        var files = new List<(string RelativePath, long Size, Func<Stream> Open)>();
        foreach (var file in inner.GetAllFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = NormalizePkgToolPath(file.FullName);
            path = RemoveContainerRoot(path, "uroot");
            path = RemoveContainerRoot(path, "Image0");
            files.Add((path, file.size, () => new PfsFileStream(file)));
        }

        foreach (var file in outer.GetAllFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            cancellationToken.ThrowIfCancellationRequested();
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
        var filePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segments = SafeExtractionPath.GetSegments(item.RelativePath);
            if (!filePaths.Add(item.RelativePath) || directories.ContainsKey(item.RelativePath))
                throw new PkgExtractionException($"Caminho duplicado no PKG: {item.RelativePath}");
            var directory = "";
            foreach (var segment in segments[..^1])
            {
                directory = directory.Length == 0 ? segment : directory + "/" + segment;
                if (filePaths.Contains(directory) ||
                    (directories.TryGetValue(directory, out var existing) && existing != directory))
                    throw new PkgExtractionException($"Conflito entre caminhos no PKG: {directory}");
                directories.TryAdd(directory, directory);
            }
        }
        long count = 0, bytes = 0;
        var totalBytes = files.Sum(item => item.Size);
        using var buffers = new CopyBuffers(files.Any(file => file.Size >= ReadAheadMinimum));
        cancellationToken.ThrowIfCancellationRequested();
        var destination = request.Destination ?? new LocalExtractionDestination(request.DestinationDirectory, request.FailOnExistingFiles);
        var preparationTime = Stopwatch.GetElapsedTime(preparationStarted);
        long readTicks = 0, writeTicks = 0, destinationTicks = 0;
        async ValueTask<int> ReadChunkAsync(Stream source, byte[] buffer, long remaining)
        {
            var start = Stopwatch.GetTimestamp();
            int read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(CopyBlockSize, remaining)),
                cancellationToken).ConfigureAwait(false);
            readTicks += Stopwatch.GetTimestamp() - start;
            return read;
        }

        async ValueTask WriteChunkAsync(Stream destination, byte[] buffer, int count)
        {
            var start = Stopwatch.GetTimestamp();
            await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            writeTicks += Stopwatch.GetTimestamp() - start;
        }

        foreach (var item in files)
        {
            if (request.PauseGate is not null) await request.PauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Stream? target = null;
            try
            {
                var relativePath = item.RelativePath;
                var operationStarted = Stopwatch.GetTimestamp();
                target = await destination.CreateTemporaryFileAsync(relativePath, cancellationToken).ConfigureAwait(false);
                destinationTicks += Stopwatch.GetTimestamp() - operationStarted;
                using var source = item.Open();
                long fileBytes = 0;
                var buffer = buffers.First;
                var spare = item.Size >= ReadAheadMinimum ? buffers.Second : null;
                int? prefetched = null;
                var lastReport = System.Diagnostics.Stopwatch.StartNew();
                progress?.Report(new ExtractionProgress("Extraindo", relativePath, count, files.Count, bytes, totalBytes));
                while (fileBytes < item.Size)
                {
                    if (request.PauseGate is not null) await request.PauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = prefetched ?? await ReadChunkAsync(source, buffer, item.Size - fileBytes).ConfigureAwait(false);
                    prefetched = null;
                    if (read == 0) break;
                    var remaining = item.Size - fileBytes - read;
                    if (spare is not null && remaining > 0 && request.PauseGate?.IsPaused != true)
                    {
                        // At most one read and one write are in flight. A
                        // failed write must drain the read before returning
                        // memory to the pool or disposing the source.
                        var writing = WriteChunkAsync(target, buffer, read);
                        var reading = ReadChunkAsync(source, spare, remaining);
                        try { await writing.ConfigureAwait(false); }
                        catch
                        {
                            try { await reading.ConfigureAwait(false); } catch { }
                            throw;
                        }
                        prefetched = await reading.ConfigureAwait(false);
                        (buffer, spare) = (spare, buffer);
                    }
                    else
                    {
                        await WriteChunkAsync(target, buffer, read).ConfigureAwait(false);
                    }
                    fileBytes += read;
                    if (lastReport.ElapsedMilliseconds >= 150 || fileBytes == item.Size)
                    {
                        progress?.Report(new ExtractionProgress("Extraindo", relativePath, count, files.Count, bytes + fileBytes, totalBytes));
                        lastReport.Restart();
                    }
                }

                if (fileBytes != item.Size)
                    throw new PkgExtractionException($"Arquivo truncado: {relativePath} ({fileBytes}/{item.Size} bytes).");

                if (request.PauseGate is not null) await request.PauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                operationStarted = Stopwatch.GetTimestamp();
                await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                await destination.CommitAsync(target, relativePath, cancellationToken).ConfigureAwait(false);
                destinationTicks += Stopwatch.GetTimestamp() - operationStarted;
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
        return new ExtractionResult(count, bytes)
        {
            // The accumulated read and write timings can overlap.
            PerformanceSummary = FormattableString.Invariant(
                $"Performance: preparation={preparationTime.TotalSeconds:F2}s; read+decrypt+decompress={(double)readTicks / Stopwatch.Frequency:F2}s; write={(double)writeTicks / Stopwatch.Frequency:F2}s; create+commit={(double)destinationTicks / Stopwatch.Frequency:F2}s; PKG-read={(double)timings.PackageReadTicks / Stopwatch.Frequency:F2}s; AES-XTS={(double)timings.DecryptionTicks / Stopwatch.Frequency:F2}s; deflate={(double)timings.DecompressionTicks / Stopwatch.Frequency:F2}s")
        };
    }

    private sealed class CopyBuffers(bool readAhead) : IDisposable
    {
        public byte[] First { get; } = ArrayPool<byte>.Shared.Rent(CopyBlockSize);
        public byte[]? Second { get; } = readAhead ? ArrayPool<byte>.Shared.Rent(CopyBlockSize) : null;

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(First);
            if (Second is not null) ArrayPool<byte>.Shared.Return(Second);
        }
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
        var keySeed = entry.KeyIndex == 3
            ? Crypto.RSA2048Decrypt(package.EntryKeys.Keys[3].key, RSAKeyset.PkgDerivedKey3Keyset)
            : Crypto.ComputeKeys(package.Header.content_id, passcode!, entry.KeyIndex);
        var keyAndIv = SHA256.HashData(entry.GetBytes().Concat(keySeed).ToArray());
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        var decryptor = aes.CreateDecryptor(keyAndIv[16..], keyAndIv[..16]);
        var encrypted = new PkgStreamAccessor(stream, entry.DataOffset, readSize);
        return new CryptoStream(encrypted, decryptor, CryptoStreamMode.Read);
    }

    private static Pkg ReadPackage(Stream stream, CancellationToken cancellationToken)
    {
        try { return new PkgReader(stream).ReadPkg(readOptionalEntries: false, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new PkgExtractionException("O arquivo não é um PKG válido ou está corrompido.", ex);
        }
    }

    private sealed class PfsFileStream : Stream
    {
        private readonly IMemoryReader view;
        private long position;
        public PfsFileStream(PfsReader.File file)
        {
            Length = file.size;
            view = file.GetView();
        }
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { var allowed = (int)Math.Min(count, Length - position); if (allowed <= 0) return 0; view.Read(position, buffer, offset, allowed); position += allowed; return allowed; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
                return ValueTask.FromResult(Read(segment.Array!, segment.Offset, segment.Count));
            return base.ReadAsync(buffer, cancellationToken);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) view.Dispose();
            base.Dispose(disposing);
        }
        public override long Seek(long offset, SeekOrigin origin) { var next = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => position + offset, SeekOrigin.End => Length + offset, _ => throw new ArgumentOutOfRangeException(nameof(origin)) }; if (next < 0 || next > Length) throw new IOException("Seek fora dos limites do arquivo PFS."); position = next; return position; }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class PkgMemoryReader : IMemoryReader
    {
        private readonly Stream stream; private readonly long start, length;
        private readonly ReadTimings timings;
        private readonly CancellationToken cancellationToken;
        public PkgMemoryReader(Stream stream, long start, long length, ReadTimings timings, CancellationToken cancellationToken)
        {
            this.stream = stream;
            this.start = start;
            this.length = length;
            this.timings = timings;
            this.cancellationToken = cancellationToken;
        }
        public void Read(long position, byte[] buffer, int offset, int count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (position < 0 || count < 0 || position > length - count) throw new EndOfStreamException("Leitura fora dos limites do PKG.");
            var started = Stopwatch.GetTimestamp();
            // Do not invalidate BufferedStream's read-ahead for sequential sectors.
            if (stream.Position != start + position)
                stream.Seek(start + position, SeekOrigin.Begin);
            var read = 0;
            while (read < count)
            {
                var n = stream.Read(buffer, offset + read, count - read);
                if (n == 0) throw new EndOfStreamException("PKG truncado.");
                read += n;
            }
            timings.PackageReadTicks += Stopwatch.GetTimestamp() - started;
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
        public override int Read(byte[] buffer, int offset, int count) { var allowed = (int)Math.Min(count, length - position); if (allowed <= 0) return 0; var absolute = start + position; if (inner.Position != absolute) inner.Position = absolute; var n = inner.Read(buffer, offset, allowed); position += n; return n; }
        public override long Seek(long offset, SeekOrigin origin) { var next = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => position + offset, SeekOrigin.End => length + offset, _ => throw new ArgumentOutOfRangeException(nameof(origin)) }; if (next < 0 || next > length) throw new IOException("Seek fora dos limites do PFS."); position = next; return position; }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
