using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OrbisExtract.Core;

public sealed record ExtractionRequest(
    Stream Package,
    string DestinationDirectory,
    string? Passcode = null,
    bool FailOnExistingFiles = true,
    IExtractionDestination? Destination = null);

public sealed record ExtractionProgress(
    string State,
    string? CurrentPath,
    long FilesCompleted,
    long? FilesTotal,
    long BytesCompleted,
    long? BytesTotal);

public sealed record ExtractionResult(long FilesExtracted, long BytesExtracted);

public interface IExtractionDestination
{
    Task<Stream> CreateTemporaryFileAsync(string relativePath, CancellationToken cancellationToken = default);
    Task CommitAsync(Stream temporaryFile, string relativePath, CancellationToken cancellationToken = default);
    Task DeleteTemporaryFileAsync(Stream temporaryFile, CancellationToken cancellationToken = default);
}

public sealed class LocalExtractionDestination : IExtractionDestination
{
    private readonly string root;
    private readonly bool failOnExistingFiles;

    public LocalExtractionDestination(string root, bool failOnExistingFiles = true)
    {
        this.root = Path.GetFullPath(root);
        this.failOnExistingFiles = failOnExistingFiles;
        Directory.CreateDirectory(this.root);
    }

    public Task<Stream> CreateTemporaryFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = SafeExtractionPath.Resolve(root, relativePath);
        if (failOnExistingFiles && File.Exists(output))
            throw new PkgExtractionException($"O arquivo já existe: {output}");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temporary = output + ".orbisextract.tmp";
        return Task.FromResult<Stream>(new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true));
    }

    public Task CommitAsync(Stream temporaryFile, string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (temporaryFile is not FileStream file)
            throw new PkgExtractionException("Destino local recebeu um temporário inválido.");
        var output = SafeExtractionPath.Resolve(root, relativePath);
        file.Flush();
        file.Dispose();
        File.Move(file.Name, output, overwrite: false);
        return Task.CompletedTask;
    }

    public Task DeleteTemporaryFileAsync(Stream temporaryFile, CancellationToken cancellationToken = default)
    {
        if (temporaryFile is FileStream file)
        {
            var name = file.Name;
            file.Dispose();
            if (File.Exists(name)) File.Delete(name);
        }
        return Task.CompletedTask;
    }
}

public interface IPkgExtractor
{
    Task<ExtractionResult> ExtractAsync(ExtractionRequest request, IProgress<ExtractionProgress>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class PkgExtractionException : Exception
{
    public PkgExtractionException(string message) : base(message) { }
    public PkgExtractionException(string message, Exception innerException) : base(message, innerException) { }
}

public static class SafeExtractionPath
{
    public static string Resolve(string destinationDirectory, string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath)) throw new PkgExtractionException("O pacote contém um caminho vazio.");
        var normalized = packagePath.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (normalized.StartsWith('/') || Path.IsPathRooted(packagePath) || segments.Any(segment => segment == "..")) throw new PkgExtractionException($"Caminho interno inseguro: {packagePath}");
        var fullDestination = Path.GetFullPath(destinationDirectory);
        var fullPath = Path.GetFullPath(Path.Combine(fullDestination, normalized));
        var relative = Path.GetRelativePath(fullDestination, fullPath);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative)) throw new PkgExtractionException($"Caminho interno fora do destino: {packagePath}");
        return fullPath;
    }
}
