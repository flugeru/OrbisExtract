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
        var ekpfs = package.GetEkpfs();
        if (ekpfs is null && string.IsNullOrEmpty(request.Passcode))
            throw new PkgExtractionException("Este PKG é criptografado ou exige passcode.");

        cancellationToken.ThrowIfCancellationRequested();
        request.Package.Position = 0;
        using var accessor = new PkgMemoryReader(request.Package, (long)package.Header.pfs_image_offset, (long)package.Header.pfs_image_size);
        var outer = new PfsReader(accessor, package.Header.pfs_flags, ekpfs);
        var inner = new PfsReader(new PFSCReader(outer.GetFile("pfs_image.dat").GetView()));
        var files = inner.GetAllFiles().ToArray();
        long count = 0, bytes = 0;
        var totalBytes = files.Sum(file => (long)file.size);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = request.Destination ?? new LocalExtractionDestination(request.DestinationDirectory, request.FailOnExistingFiles);
            Stream? target = null;
            try
            {
                var relativePath = file.FullName.TrimStart('/', '\\');
                if (string.IsNullOrWhiteSpace(relativePath))
                    throw new PkgExtractionException("O pacote contém um caminho interno vazio.");

                progress?.Report(new ExtractionProgress("Extraindo", relativePath, count, files.Length, bytes, totalBytes));
                target = await destination.CreateTemporaryFileAsync(relativePath, cancellationToken).ConfigureAwait(false);
                using var source = new PfsFileStream(file);
                await source.CopyToAsync(target, 128 * 1024, cancellationToken).ConfigureAwait(false);
                await target.FlushAsync(cancellationToken).ConfigureAwait(false);
                await destination.CommitAsync(target, relativePath, cancellationToken).ConfigureAwait(false);
                target = null;
                count++;
                bytes += file.size;
                progress?.Report(new ExtractionProgress("Extraindo", relativePath, count, files.Length, bytes, totalBytes));
            }
            catch
            {
                if (target is not null)
                    await destination.DeleteTemporaryFileAsync(target, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        progress?.Report(new ExtractionProgress("Concluído", null, count, files.Length, bytes, totalBytes));
        return new ExtractionResult(count, bytes);
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
