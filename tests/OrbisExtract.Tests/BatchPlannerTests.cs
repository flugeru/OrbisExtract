using OrbisExtract.Core;

namespace OrbisExtract.Tests;

public sealed class BatchPlannerTests
{
    [Fact]
    public void ExactContentIdDisambiguatesBaseMatches()
    {
        var plan = BatchPlanner.Create([
            Package(0, "CUSA00001", "other-edition", PackageKind.BaseGame, "01.00"),
            Package(1, "CUSA00001", "matching-edition", PackageKind.BaseGame, "01.00"),
            Package(2, "CUSA00001", "matching-edition", PackageKind.Update, "01.23")
        ]);
        Assert.Empty(plan.Issues);
        Assert.Equal(2, plan.Groups.Count);
        Assert.Equal(new[] { 1, 2 }, plan.Groups.Single(group => group.Packages.Count > 1).Packages.Select(package => package.Index));
    }

    [Fact]
    public void OrphansDeltasAndAmbiguousVersionsLeaveIndependentGamesAvailable()
    {
        var plan = BatchPlanner.Create([
            Package(0, "CUSA00001", "a", PackageKind.BaseGame, "01.00"),
            Package(1, "CUSA00001", "a", PackageKind.Update, "01.02"),
            Package(2, "CUSA00001", "a", PackageKind.Update, "1.2.0"),
            Package(3, "CUSA00002", "b", PackageKind.Update, "01.02"),
            Package(4, "CUSA00003", "c", PackageKind.BaseGame, "01.00"),
            Package(5, "CUSA00003", "c", PackageKind.DeltaUpdate, "01.02"),
            Package(6, "CUSA00004", "d", PackageKind.BaseGame, "01.00")
        ]);
        Assert.Equal(6, Assert.Single(Assert.Single(plan.Groups).Packages).Index);
        Assert.Equal(3, plan.Issues.Count);
    }

    [Fact]
    public void WorkerLimitAdaptsToCpuMemoryAndProvider()
    {
        const long GiB = 1024L * 1024 * 1024;
        Assert.Equal(2, ParallelExtraction.WorkerLimit(4, GiB, true, false));
        Assert.Equal(3, ParallelExtraction.WorkerLimit(8, GiB, true, false));
        Assert.True(ParallelExtraction.WorkerLimit(16, 4 * GiB, false, false) > 3);
        Assert.Equal(1, ParallelExtraction.WorkerLimit(16, 4 * GiB, false, true));
        Assert.Equal(1, ParallelExtraction.WorkerLimit(16, GiB / 8, false, false));
        Assert.Equal(2, ParallelExtraction.WorkerLimit(4, GiB / 8, true, false, 48 * 1024 * 1024));
        Assert.Equal(1, ParallelExtraction.WorkerLimit(4, GiB / 8, true, false, 192 * 1024 * 1024));
    }

    private static BatchPackage Package(int index, string titleId, string contentId, PackageKind kind, string version) =>
        new(index, $"package-{index}.pkg", new PackageMetadata(titleId, version, contentId)
        {
            TitleId = titleId,
            Kind = kind,
            ApplicationVersion = Version.Parse(version)
        });
}
