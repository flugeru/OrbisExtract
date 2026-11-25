using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OrbisExtract.Core;

public sealed record ExtractionRequest(
    Stream Package,
    string DestinationDirectory,
    string? Passcode = null,
    bool FailOnExistingFiles = true,
    IExtractionDestination? Destination = null,
    ExtractionPauseGate? PauseGate = null,
    int MaxFileWorkers = 1,
    bool ResumeExistingFiles = false,
    bool VerifyPackageIntegrity = false);

public sealed class ExtractionPauseGate
{
    private readonly object _sync = new();
    private TaskCompletionSource<bool>? _resume;
    public bool IsPaused { get { lock (_sync) return _resume is not null; } }
    public void Pause() { lock (_sync) _resume ??= new(TaskCreationOptions.RunContinuationsAsynchronously); }
    public void Resume()
    {
        TaskCompletionSource<bool>? pending;
        lock (_sync) { pending = _resume; _resume = null; }
        pending?.TrySetResult(true);
    }
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task? pending;
            lock (_sync) pending = _resume?.Task;
            if (pending is null) return;
            await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed record ExtractionProgress(
    string State,
    string? CurrentPath,
    long FilesCompleted,
    long? FilesTotal,
    long BytesCompleted,
    long? BytesTotal);

public sealed record ExtractionResult(long FilesExtracted, long BytesExtracted)
{
    public string? PerformanceSummary { get; init; }
}

public interface IExtractionDestination
{
    Task<Stream> CreateTemporaryFileAsync(string relativePath, CancellationToken cancellationToken = default);
    Task CommitAsync(Stream temporaryFile, string relativePath, CancellationToken cancellationToken = default);
    Task DeleteTemporaryFileAsync(Stream temporaryFile, CancellationToken cancellationToken = default);
}

public interface IResumableExtractionDestination : IExtractionDestination
{
    Task<Stream?> OpenExistingFileAsync(string relativePath, CancellationToken cancellationToken = default);
}

public sealed class LocalExtractionDestination : IResumableExtractionDestination
{
    private readonly string root;
    private readonly bool failOnExistingFiles;

    public LocalExtractionDestination(string root, bool failOnExistingFiles = true)
    {
        this.root = Path.GetFullPath(root);
        this.failOnExistingFiles = failOnExistingFiles;
        Directory.CreateDirectory(this.root);
    }

    public void CheckAvailableSpace(long requiredBytes)
    {
        if (requiredBytes <= 0 || root.StartsWith(@"\\", System.StringComparison.Ordinal)) return;
        long available;
        try
        {
            // On Unix the destination may be on a nested mount rather than '/'.
            var comparison = System.OperatingSystem.IsWindows()
                ? System.StringComparison.OrdinalIgnoreCase : System.StringComparison.Ordinal;
            DriveInfo? match = null;
            var path = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
            foreach (var drive in DriveInfo.GetDrives())
            {
                var prefix = Path.EndsInDirectorySeparator(drive.Name) ? drive.Name : drive.Name + Path.DirectorySeparatorChar;
                if (path.StartsWith(prefix, comparison) && (match is null || drive.Name.Length > match.Name.Length))
                    match = drive;
            }
            if (match is null) return;
            available = match.AvailableFreeSpace;
        }
        catch (IOException) { return; }
        catch (System.UnauthorizedAccessException) { return; }
        if (requiredBytes > available)
            throw new PkgExtractionException($"Espaço insuficiente no destino. Necessário: {requiredBytes:N0} bytes; disponível: {available:N0} bytes.");
    }

    public Task<Stream?> OpenExistingFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = SafeExtractionPath.Resolve(root, relativePath);
        EnsureNoReparsePoints(relativePath);
        if (Directory.Exists(output))
            throw new PkgExtractionException($"Uma pasta ocupa o caminho do arquivo: {relativePath}");
        try
        {
            Stream file = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan | FileOptions.Asynchronous);
            return Task.FromResult<Stream?>(file);
        }
        catch (FileNotFoundException) { return Task.FromResult<Stream?>(null); }
        catch (DirectoryNotFoundException) { return Task.FromResult<Stream?>(null); }
    }

    public Task<Stream> CreateTemporaryFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = SafeExtractionPath.Resolve(root, relativePath);
        EnsureNoReparsePoints(relativePath);
        if (Directory.Exists(output) || (failOnExistingFiles && File.Exists(output)))
            throw new PkgExtractionException($"O arquivo já existe: {output}");
        var directory = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".orbisextract-{System.Guid.NewGuid():N}.tmp");
        return Task.FromResult<Stream>(new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true));
    }

    public Task CommitAsync(Stream temporaryFile, string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (temporaryFile is not FileStream file)
            throw new PkgExtractionException("Destino local recebeu um temporário inválido.");
        var output = SafeExtractionPath.Resolve(root, relativePath);
        file.Dispose();
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNoReparsePoints(relativePath);
        File.Move(file.Name, output, overwrite: !failOnExistingFiles);
        return Task.CompletedTask;
    }

    public Task DeleteTemporaryFileAsync(Stream temporaryFile, CancellationToken cancellationToken = default)
    {
        if (temporaryFile is FileStream file)
        {
            var name = file.Name;
            try { file.Dispose(); }
            finally { File.Delete(name); }
        }
        return Task.CompletedTask;
    }

    private void EnsureNoReparsePoints(string relativePath)
    {
        RejectReparsePoint(root);
        var current = root;
        foreach (var segment in SafeExtractionPath.GetSegments(relativePath))
        {
            current = Path.Combine(current, segment);
            RejectReparsePoint(current);
        }
    }

    private static void RejectReparsePoint(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new PkgExtractionException($"O destino contém um link ou ponto de redirecionamento: {path}");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
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
    public static string[] GetSegments(string packagePath)
    {
        var segments = packagePath.Replace('\\', '/').Split('/');
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment is "." or ".." ||
                segment.Contains(':') || segment.Contains('\0') ||
                (System.OperatingSystem.IsWindows() && IsInvalidWindowsName(segment)))
                throw new PkgExtractionException($"Caminho interno inseguro: {packagePath}");
        }
        return segments;
    }

    public static string Resolve(string destinationDirectory, string packagePath)
    {
        var fullDestination = Path.GetFullPath(destinationDirectory);
        return Path.Combine(fullDestination, Path.Combine(GetSegments(packagePath)));
    }

    private static bool IsInvalidWindowsName(string segment)
    {
        if (segment.EndsWith('.') || segment.EndsWith(' ') || segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return true;

        var dot = segment.IndexOf('.');
        var stem = (dot < 0 ? segment : segment[..dot]).TrimEnd(' ').ToUpperInvariant();
        return stem is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            (stem.Length == 4 && (stem.StartsWith("COM", System.StringComparison.Ordinal) ||
             stem.StartsWith("LPT", System.StringComparison.Ordinal)) && "123456789¹²³".Contains(stem[3]));
    }
}
