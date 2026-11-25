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
        if (request.VerifyPackageIntegrity)
        {
            var header = package.Header;
            if (header.body_offset > packageLength || header.body_size > packageLength - header.body_offset)
                throw new PkgExtractionException("Corpo fora dos limites do PKG.");
            progress?.Report(new ExtractionProgress("Verificando", null, 0, null, 0, null));
            await VerifyRegionAsync(header.body_offset, header.body_size, header.body_digest, "corpo", request, cancellationToken).ConfigureAwait(false);
            await VerifyRegionAsync(header.pfs_image_offset, header.pfs_image_size, header.pfs_image_digest, "PFS", request, cancellationToken).ConfigureAwait(false);
        }

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
        var timings = new ReadTimings();
        var packageSource = new PackageSource(request.Package);
        var imageLength = (long)package.Header.pfs_image_size;
        using var accessor = new PkgMemoryReader(packageSource, (long)package.Header.pfs_image_offset, imageLength, timings, cancellationToken);
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
                () => OpenPkgEntry(packageSource, package, entry, passcode, readSize)));
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
        var totalFiles = files.Count;
        var totalBytes = files.Sum(item => item.Size);
        var progressLock = new object();
        cancellationToken.ThrowIfCancellationRequested();
        var destination = request.Destination ?? new LocalExtractionDestination(request.DestinationDirectory, request.FailOnExistingFiles);
        var localDestination = destination as LocalExtractionDestination;
        if (request.ResumeExistingFiles)
        {
            if (destination is not IResumableExtractionDestination resumable)
                throw new PkgExtractionException("Este destino não permite verificar arquivos existentes.");

            var pending = new List<(string RelativePath, long Size, Func<Stream> Open)>();
            foreach (var item in files)
            {
                if (request.PauseGate is not null) await request.PauseGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                using var existing = await resumable.OpenExistingFileAsync(item.RelativePath, cancellationToken).ConfigureAwait(false);
                if (existing is null)
                {
                    pending.Add(item);
                    continue;
                }
                // Compare with the current package, not a size, timestamp, or untrusted sidecar.
                if (existing.CanSeek && existing.Length != item.Size)
                    throw ExistingFileMismatch(item.RelativePath);
                using var source = item.Open();
                var sourceHash = await HashContentsAsync(source, item.Size, request.PauseGate, cancellationToken).ConfigureAwait(false);
                byte[] existingHash;
                try
                {
                    existingHash = await HashContentsAsync(existing, item.Size, request.PauseGate, cancellationToken, requireEof: true).ConfigureAwait(false);
                }
                catch (EndOfStreamException) { throw ExistingFileMismatch(item.RelativePath); }
                if (!CryptographicOperations.FixedTimeEquals(sourceHash, existingHash))
                    throw ExistingFileMismatch(item.RelativePath);
                count++;
                bytes += item.Size;
                progress?.Report(new ExtractionProgress("Verificando", item.RelativePath, count, totalFiles, bytes, totalBytes));
            }
            files = pending;
        }

        localDestination?.CheckAvailableSpace(totalBytes - bytes);
        // Only opt in when both ends support independent file operations.
        var workers = packageSource.SupportsPositionalReads && localDestination is not null
            ? Math.Min(files.Count, Math.Clamp(request.MaxFileWorkers, 1, 4)) : 1;
        var preparationTime = Stopwatch.GetElapsedTime(preparationStarted);
        long readTicks = 0, writeTicks = 0, destinationTicks = 0;
        void Report(string path, long written = 0, bool completed = false)
        {
            lock (progressLock)
            {
                bytes += written;
                if (completed) count++;
                progress?.Report(new ExtractionProgress("Extraindo", path, count, totalFiles, bytes, totalBytes));
            }
        }
        async ValueTask<int> ReadChunkAsync(Stream source, byte[] buffer, long remaining, CancellationToken token)
        {
            var start = Stopwatch.GetTimestamp();
            int read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(Math.Min(CopyBlockSize, buffer.Length), remaining)),
                token).ConfigureAwait(false);
            Interlocked.Add(ref readTicks, Stopwatch.GetTimestamp() - start);
            return read;
        }

        async ValueTask WriteChunkAsync(Stream destination, byte[] buffer, int count, CancellationToken token)
        {
            var start = Stopwatch.GetTimestamp();
            await destination.WriteAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            Interlocked.Add(ref writeTicks, Stopwatch.GetTimestamp() - start);
        }

        async ValueTask ExtractFileAsync((string RelativePath, long Size, Func<Stream> Open) item, CopyBuffers buffers, CancellationToken token)
        {
            if (request.PauseGate is not null) await request.PauseGate.WaitAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            Stream? target = null;
            try
            {
                var relativePath = item.RelativePath;
                var operationStarted = Stopwatch.GetTimestamp();
                target = await destination.CreateTemporaryFileAsync(relativePath, token).ConfigureAwait(false);
                Interlocked.Add(ref destinationTicks, Stopwatch.GetTimestamp() - operationStarted);
                using var source = item.Open();
                long fileBytes = 0;
                var buffer = buffers.GetFirst(item.Size);
                var spare = item.Size >= ReadAheadMinimum ? buffers.Second : null;
                int? prefetched = null;
                var lastReport = Stopwatch.StartNew();
                long reportedFileBytes = 0;
                Report(relativePath);
                while (fileBytes < item.Size)
                {
                    if (request.PauseGate is not null) await request.PauseGate.WaitAsync(token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    var read = prefetched ?? await ReadChunkAsync(source, buffer, item.Size - fileBytes, token).ConfigureAwait(false);
                    prefetched = null;
                    if (read == 0) break;
                    var remaining = item.Size - fileBytes - read;
                    if (spare is not null && remaining > 0 && request.PauseGate?.IsPaused != true)
                    {
                        // At most one read and one write are in flight. A
                        // failed write must drain the read before returning
                        // memory to the pool or disposing the source.
                        var writing = WriteChunkAsync(target, buffer, read, token);
                        var reading = ReadChunkAsync(source, spare, remaining, token);
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
                        await WriteChunkAsync(target, buffer, read, token).ConfigureAwait(false);
                    }
                    fileBytes += read;
                    if (lastReport.ElapsedMilliseconds >= 150 || fileBytes == item.Size)
                    {
                        Report(relativePath, fileBytes - reportedFileBytes);
                        reportedFileBytes = fileBytes;
                        lastReport.Restart();
                    }
                }

                if (fileBytes != item.Size)
                    throw new PkgExtractionException($"Arquivo truncado: {relativePath} ({fileBytes}/{item.Size} bytes).");

                if (request.PauseGate is not null) await request.PauseGate.WaitAsync(token).ConfigureAwait(false);
                operationStarted = Stopwatch.GetTimestamp();
                await target.FlushAsync(token).ConfigureAwait(false);
                await destination.CommitAsync(target, relativePath, token).ConfigureAwait(false);
                Interlocked.Add(ref destinationTicks, Stopwatch.GetTimestamp() - operationStarted);
                target = null;
                Report(relativePath, fileBytes - reportedFileBytes, completed: true);
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
        if (workers > 1)
        {
            int next = -1;
            await Parallel.ForEachAsync(Enumerable.Range(0, workers), new ParallelOptions
            {
                MaxDegreeOfParallelism = workers,
                CancellationToken = cancellationToken,
            }, async (_, token) =>
            {
                using var buffers = new CopyBuffers();
                int index;
                while (!token.IsCancellationRequested && (index = Interlocked.Increment(ref next)) < files.Count)
                    await ExtractFileAsync(files[index], buffers, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
        else if (files.Count != 0)
        {
            using var buffers = new CopyBuffers();
            foreach (var item in files)
                await ExtractFileAsync(item, buffers, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new ExtractionProgress("Concluído", null, count, totalFiles, bytes, null));
        return new ExtractionResult(count, bytes)
        {
            // The accumulated read and write timings can overlap across files.
            PerformanceSummary = FormattableString.Invariant(
                $"Performance: file-workers={(files.Count == 0 ? 0 : workers)}; preparation={preparationTime.TotalSeconds:F2}s; read+decrypt+decompress={(double)readTicks / Stopwatch.Frequency:F2}s; write={(double)writeTicks / Stopwatch.Frequency:F2}s; create+commit={(double)destinationTicks / Stopwatch.Frequency:F2}s; PKG-read={(double)timings.PackageReadTicks / Stopwatch.Frequency:F2}s; AES-XTS={(double)timings.DecryptionTicks / Stopwatch.Frequency:F2}s; deflate={(double)timings.DecompressionTicks / Stopwatch.Frequency:F2}s")
        };
    }

    private static async Task VerifyRegionAsync(ulong start, ulong size, byte[] expected, string name,
        ExtractionRequest request, CancellationToken token)
    {
        if (size == 0 || expected.Length != 32 || !expected.Any(value => value != 0))
            throw new PkgExtractionException($"O PKG não fornece um SHA-256 verificável para {name}.");
        request.Package.Position = (long)start;
        var actual = await HashContentsAsync(request.Package, (long)size, request.PauseGate, token).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new PkgExtractionException($"Falha na verificação SHA-256 do PKG ({name}).");
    }

    private static PkgExtractionException ExistingFileMismatch(string path) =>
        new($"Arquivo existente diferente do PKG: {path}. Escolha outra pasta ou mova o arquivo antes de retomar.");

    private static async Task<byte[]> HashContentsAsync(Stream source, long size, ExtractionPauseGate? pauseGate,
        CancellationToken token, bool requireEof = false)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent((int)Math.Clamp(size, 1, CopyBlockSize));
        try
        {
            while (size > 0)
            {
                if (pauseGate is not null) await pauseGate.WaitAsync(token).ConfigureAwait(false);
                int read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(size, CopyBlockSize)), token).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("Arquivo truncado durante a verificação.");
                hash.AppendData(buffer, 0, read);
                size -= read;
            }
            if (requireEof && await source.ReadAsync(buffer.AsMemory(0, 1), token).ConfigureAwait(false) != 0)
                throw new EndOfStreamException("O arquivo contém bytes além do tamanho esperado.");
            return hash.GetHashAndReset();
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    private sealed class CopyBuffers : IDisposable
    {
        private byte[]? first, second;
        public byte[] GetFirst(long fileSize)
        {
            if (fileSize == 0) return Array.Empty<byte>();
            int size = (int)Math.Clamp(fileSize, 4096, CopyBlockSize);
            if (first is null || first.Length < size)
            {
                var next = ArrayPool<byte>.Shared.Rent(size);
                if (first is not null) ArrayPool<byte>.Shared.Return(first);
                first = next;
            }
            return first;
        }

        public byte[] Second => second ??= ArrayPool<byte>.Shared.Rent(CopyBlockSize);

        public void Dispose()
        {
            if (first is not null) ArrayPool<byte>.Shared.Return(first);
            if (second is not null) ArrayPool<byte>.Shared.Return(second);
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

    private static Stream OpenPkgEntry(PackageSource stream, Pkg package, MetaEntry entry, string? passcode, long readSize)
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

    private sealed class PackageSource(Stream stream)
    {
        // Android document providers may expose unusual descriptors. Keep them on
        // the buffered stream path; use an OS file offset only for desktop files.
        private readonly Microsoft.Win32.SafeHandles.SafeFileHandle? handle =
            !OperatingSystem.IsAndroid() && stream is FileStream file && stream.GetType() == typeof(FileStream)
                ? file.SafeFileHandle : null;
        public bool SupportsPositionalReads => handle is not null;

        public int Read(long position, byte[] buffer, int offset, int count)
        {
            if (handle is not null)
                return RandomAccess.Read(handle, buffer.AsSpan(offset, count), position);
            if (stream.Position != position) stream.Seek(position, SeekOrigin.Begin);
            return stream.Read(buffer, offset, count);
        }
    }

    private sealed class PkgMemoryReader : IMemoryReader
    {
        private readonly PackageSource stream; private readonly long start, length;
        private readonly ReadTimings timings;
        private readonly CancellationToken cancellationToken;
        public PkgMemoryReader(PackageSource stream, long start, long length, ReadTimings timings, CancellationToken cancellationToken)
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
            var read = 0;
            while (read < count)
            {
                var n = stream.Read(start + position + read, buffer, offset + read, count - read);
                if (n == 0) throw new EndOfStreamException("PKG truncado.");
                read += n;
            }
            Interlocked.Add(ref timings.PackageReadTicks, Stopwatch.GetTimestamp() - started);
        }
        public void Dispose() { }
    }

    private sealed class PkgStreamAccessor : Stream
    {
        private readonly PackageSource inner; private readonly long start, length; private long position;
        public PkgStreamAccessor(PackageSource inner, long start, long length) { this.inner = inner; this.start = start; this.length = length; }
        public override bool CanRead => true; public override bool CanSeek => true; public override bool CanWrite => false;
        public override long Length => length; public override long Position { get => position; set => Seek(value, SeekOrigin.Begin); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) { var allowed = (int)Math.Min(count, length - position); if (allowed <= 0) return 0; var n = inner.Read(start + position, buffer, offset, allowed); position += n; return n; }
        public override long Seek(long offset, SeekOrigin origin) { var next = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => position + offset, SeekOrigin.End => length + offset, _ => throw new ArgumentOutOfRangeException(nameof(origin)) }; if (next < 0 || next > length) throw new IOException("Seek fora dos limites do PFS."); position = next; return position; }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
