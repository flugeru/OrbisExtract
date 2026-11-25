using System.Text;

namespace OrbisExtract.Core;

public sealed record BatchPackage(int Index, string Name, PackageMetadata Metadata);
public sealed record PackageGroup(string OutputName, IReadOnlyList<BatchPackage> Packages);
public sealed record BatchIssue(string Package, string Reason);
public sealed record BatchPlan(IReadOnlyList<PackageGroup> Groups, IReadOnlyList<BatchIssue> Issues);

public static class BatchPlanner
{
    public static BatchPlan Create(IReadOnlyList<BatchPackage> packages, bool allowStandaloneUpdate = false)
    {
        if (packages is [var single] && IsUpdate(single))
        {
            if (single.Metadata.Kind == PackageKind.DeltaUpdate)
                return new([], [new(single.Name, "Updates binários delta exigem reconstrução e não podem ser aplicados como arquivos.")]);
            if (allowStandaloneUpdate)
                return new([new(MakeFolderName(single), [single])], []);
        }

        var issues = new List<BatchIssue>();
        var roots = packages.Where(package => !IsUpdate(package)).ToArray();
        var basesByTitle = roots.Where(package => package.Metadata.Kind == PackageKind.BaseGame &&
                package.Metadata.TitleId is not null)
            .ToLookup(package => package.Metadata.TitleId!, StringComparer.OrdinalIgnoreCase);
        var updatesByBase = new Dictionary<int, List<BatchPackage>>();
        var blockedBases = new HashSet<int>();

        foreach (var update in packages.Where(IsUpdate))
        {
            var identity = update.Metadata.TitleId;
            var matching = identity is null ? Array.Empty<BatchPackage>() : basesByTitle[identity].ToArray();
            var exact = matching.Where(candidate => string.Equals(candidate.Metadata.ContentId,
                update.Metadata.ContentId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (exact.Length > 0) matching = exact;
            if (matching.Length != 1)
            {
                issues.Add(new(update.Name, matching.Length == 0
                    ? "Selecione o PKG base deste update."
                    : "Mais de um PKG base é compatível com este update."));
                foreach (var candidate in matching) blockedBases.Add(candidate.Index);
                continue;
            }

            var game = matching[0];
            if (update.Metadata.Kind == PackageKind.DeltaUpdate)
            {
                blockedBases.Add(game.Index);
                issues.Add(new(update.Name, "Updates binários delta exigem reconstrução e não podem ser aplicados como arquivos."));
                continue;
            }
            if (!updatesByBase.TryGetValue(game.Index, out var updates))
                updatesByBase.Add(game.Index, updates = []);
            updates.Add(update);
        }

        var groups = new List<PackageGroup>(roots.Length);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var game in roots)
        {
            if (blockedBases.Contains(game.Index)) continue;
            updatesByBase.TryGetValue(game.Index, out var updates);
            if (updates is not null)
            {
                if (updates.Count > 1 && (updates.Any(update => update.Metadata.ApplicationVersion is null) ||
                    updates.Select(update => OrderVersion(update.Metadata.ApplicationVersion!)).Distinct().Count() != updates.Count))
                {
                    issues.Add(new(game.Name, "Os updates precisam ter versões APP_VER distintas e válidas para definir a ordem."));
                    continue;
                }
                if (game.Metadata.ApplicationVersion is { } installed && updates.Any(update =>
                        update.Metadata.ApplicationVersion is { } version && OrderVersion(version) <= OrderVersion(installed)))
                {
                    issues.Add(new(game.Name, "O lote inclui um update da mesma versão ou anterior ao PKG base."));
                    continue;
                }
            }

            var folder = MakeFolderName(game);
            var output = folder;
            for (int suffix = 2; !usedNames.Add(output); suffix++) output = $"{folder} ({suffix})";
            var members = new List<BatchPackage> { game };
            if (updates is not null)
                members.AddRange(updates.OrderBy(update => update.Metadata.ApplicationVersion is { } version ? OrderVersion(version) : null));
            groups.Add(new(output, members));
        }
        return new(groups, issues);
    }

    private static Version OrderVersion(Version version) =>
        new(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));

    private static bool IsUpdate(BatchPackage package) =>
        package.Metadata.Kind is PackageKind.Update or PackageKind.DeltaUpdate;

    private static string MakeFolderName(BatchPackage package)
    {
        var filename = Path.GetFileNameWithoutExtension(package.Name);
        var safe = new StringBuilder();
        foreach (var character in filename)
        {
            if (safe.Length >= 80) break;
            safe.Append(char.IsLetterOrDigit(character) || character is ' ' or '_' or '-' ? character : '_');
        }
        // Prefixing also avoids reserved device names on Windows.
        var identity = package.Metadata.Kind == PackageKind.BaseGame ? package.Metadata.TitleId : null;
        var title = safe.ToString().Trim();
        return $"{identity ?? "PKG"}{(title.Length > 0 ? " - " + title : "")}";
    }
}

public sealed record BatchJobResult<T>(T Item, ExtractionResult? Extraction, Exception? Error);

public static class ParallelExtraction
{
    public static int WorkerLimit(int processors, long memoryBudget, bool providerStorage, bool memoryPressure,
        long estimatedWorkerBytes = 0)
    {
        const long MiB = 1024 * 1024;
        // CPU work includes AES and inflate. Give each active package a
        // conservative memory allowance for inode indexes and sector maps.
        int cpuLimit = processors >= 4 ? (processors + 1) / 2 : 2;
        long workerBytes = estimatedWorkerBytes > 0
            ? Math.Max(estimatedWorkerBytes, (providerStorage ? 48 : 96) * MiB)
            : (providerStorage ? 128 : 192) * MiB;
        int memoryLimit = memoryBudget > 0
            ? (int)Math.Clamp(memoryBudget / workerBytes, 1, int.MaxValue)
            : 2;
        int ioLimit = providerStorage ? Math.Max(2, (processors + 2) / 3) : Math.Max(2, processors);
        if (memoryPressure) memoryLimit = 1;
        return Math.Min(Math.Min(cpuLimit, memoryLimit), ioLimit);
    }

    public static async Task<BatchJobResult<T>[]> RunAsync<T>(
        IReadOnlyList<T> items, Func<int> getLimit, Func<T, CancellationToken, Task<ExtractionResult>> extract,
        CancellationToken cancellationToken = default)
    {
        var running = new List<Task<BatchJobResult<T>>>();
        var results = new List<BatchJobResult<T>>(items.Count);
        int next = 0;
        while (running.Count > 0 || (next < items.Count && !cancellationToken.IsCancellationRequested))
        {
            var limit = Math.Clamp(getLimit(), 1, items.Count);
            while (next < items.Count && running.Count < limit && !cancellationToken.IsCancellationRequested)
            {
                var item = items[next++];
                running.Add(Task.Run(async () =>
                {
                    try { return new BatchJobResult<T>(item, await extract(item, cancellationToken).ConfigureAwait(false), null); }
                    catch (Exception ex) { return new BatchJobResult<T>(item, null, ex); }
                }));
            }
            if (running.Count == 0) break;
            var completed = await Task.WhenAny(running).ConfigureAwait(false);
            running.Remove(completed);
            results.Add(await completed.ConfigureAwait(false));
        }
        return results.ToArray();
    }
}

public interface IExtractionWorkspace : IAsyncDisposable
{
    IExtractionDestination GetDestination(bool overwrite);
    Task PublishAsync(CancellationToken cancellationToken);
}

public sealed class LocalExtractionWorkspace : IExtractionWorkspace
{
    private readonly string stagingPath;
    private readonly string outputPath;
    private bool published;

    public LocalExtractionWorkspace(string root, string outputName)
    {
        outputPath = SafeExtractionPath.Resolve(root, outputName);
        if (Directory.Exists(outputPath) || File.Exists(outputPath))
            throw new PkgExtractionException($"O destino já existe: {outputPath}");
        stagingPath = Path.Combine(root, $".orbisextract-{Guid.NewGuid():N}.partial");
        Directory.CreateDirectory(stagingPath);
    }

    public IExtractionDestination GetDestination(bool overwrite) => new LocalExtractionDestination(stagingPath, !overwrite);

    public Task PublishAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(outputPath) || File.Exists(outputPath))
            throw new PkgExtractionException($"O destino já existe: {outputPath}");
        Directory.Move(stagingPath, outputPath);
        published = true;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (!published && Directory.Exists(stagingPath))
            Directory.Delete(stagingPath, recursive: true);
        return ValueTask.CompletedTask;
    }
}
