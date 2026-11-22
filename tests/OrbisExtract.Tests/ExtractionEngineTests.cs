using LibOrbisPkg.GP4;
using LibOrbisPkg.PFS;
using LibOrbisPkg.PKG;
using LibOrbisPkg.SFO;
using OrbisExtract.Core;
using OrbisExtract.Infrastructure.OpenOrbis;

namespace OrbisExtract.Tests;

public sealed class ExtractionEngineTests
{
    [Fact]
    public async Task ExtractsPayloadAndMetadataFromShortReadsAndPreservesExistingFolders()
    {
        using var workspace = new TestDirectory();
        var output = Path.Combine(workspace.Root, "output");
        var existing = Directory.CreateDirectory(Path.Combine(output, "test_access")).FullName;
        var marker = Path.Combine(existing, "keep.txt");
        await File.WriteAllTextAsync(marker, "keep");
        using var package = CreatePackage(out var payload);

        var metadata = await new OpenOrbisPkgMetadataReader().ReadAsync(package);
        Assert.Equal("Synthetic package", metadata.Title);
        var extractor = new OpenOrbisPkgExtractor();
        var result = await extractor.ExtractAsync(new ExtractionRequest(package, output));

        Assert.True(result.FilesExtracted >= 3);
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(output, "data", "payload.bin")));
        Assert.Empty(await File.ReadAllBytesAsync(Path.Combine(output, "data", "empty.bin")));
        Assert.True(File.Exists(Path.Combine(output, "sce_sys", "param.sfo")));
        var parsed = new PkgReader(package).ReadPkg(readOptionalEntries: false);
        var license = Assert.Single(parsed.Metas.Metas, entry => entry.id == EntryId.LICENSE_DAT);
        Assert.True(license.Encrypted);
        var ciphertext = new byte[(license.DataSize + 15) & ~15u];
        package.Position = license.DataOffset;
        package.ReadExactly(ciphertext);
        var expectedLicense = Entry.Decrypt(ciphertext, parsed, license);
        Assert.Equal(expectedLicense[..(int)license.DataSize],
            await File.ReadAllBytesAsync(Path.Combine(output, "sce_sys", "license.dat")));
        Assert.Equal("keep", await File.ReadAllTextAsync(marker));
        await Assert.ThrowsAsync<PkgExtractionException>(() => extractor.ExtractAsync(new ExtractionRequest(package, output)));
        Assert.Equal(payload, await File.ReadAllBytesAsync(Path.Combine(output, "data", "payload.bin")));
        Assert.Empty(Directory.GetFiles(output, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task CancelDuringWriteRemovesPartialFileAndPreservesCommittedFiles()
    {
        using var workspace = new TestDirectory();
        using var package = CreatePackage(out _);
        using var cancellation = new CancellationTokenSource();
        var progress = new CallbackProgress(p =>
        {
            if (p.CurrentPath == "data/payload.bin" && p.BytesCompleted > 0)
                cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OpenOrbisPkgExtractor().ExtractAsync(
            new ExtractionRequest(package, workspace.Root), progress, cancellation.Token));
        Assert.True(File.Exists(Path.Combine(workspace.Root, "data", "empty.bin")));
        Assert.False(File.Exists(Path.Combine(workspace.Root, "data", "payload.bin")));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RejectsPackageWithOversizedEntryTableBeforeCreatingDestination()
    {
        using var workspace = new TestDirectory();
        using var package = CreatePackage(out _);
        package.Position = 0x10;
        package.Write(new byte[] { 0xff, 0xff, 0xff, 0xff });
        var output = Path.Combine(workspace.Root, "output");
        await Assert.ThrowsAsync<PkgExtractionException>(() => new OpenOrbisPkgExtractor().ExtractAsync(
            new ExtractionRequest(package, output)));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public async Task CancellingWhilePausedReleasesWaiters()
    {
        var gate = new ExtractionPauseGate();
        gate.Pause();
        using var cancellation = new CancellationTokenSource();
        var waiting = gate.WaitAsync(cancellation.Token);
        Assert.False(waiting.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        gate.Resume();
        await gate.WaitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RealExtractorsOverlapAndPublishTheBaseWithUpdatesInVersionOrder()
    {
        using var workspace = new TestDirectory();
        using var first = CreatePackage(out _, VolumeType.pkg_ps4_app, 17, "01.00");
        using var latest = CreatePackage(out var expected, VolumeType.pkg_ps4_patch, 37, "01.12");
        using var older = CreatePackage(out _, VolumeType.pkg_ps4_patch, 27, "01.02");
        using var independent = CreatePackage(out var other, VolumeType.pkg_ps4_app, 41, "01.00",
            "AA0000-BBBB00001_00-ZZZZZZZZZZZZZZZZ");
        Stream[] streams = [latest, independent, older, first];
        var metadata = new OpenOrbisPkgMetadataReader();
        var inputs = new List<BatchPackage>();
        for (int i = 0; i < streams.Length; i++)
            inputs.Add(new BatchPackage(i, $"game-{i}.pkg", await metadata.ReadAsync(streams[i])));

        var plan = BatchPlanner.Create(inputs);
        Assert.Empty(plan.Issues);
        Assert.Equal(2, plan.Groups.Count);
        var overlay = plan.Groups.Single(group => group.Packages.Count > 1);
        Assert.Equal(new[] { 3, 2, 0 }, overlay.Packages.Select(package => package.Index));

        using var barrier = new Barrier(2);
        var results = await ParallelExtraction.RunAsync(plan.Groups, () => 2, async (group, token) =>
        {
            await using var staging = new LocalExtractionWorkspace(workspace.Root, group.OutputName);
            var extractor = new OpenOrbisPkgExtractor();
            for (int i = 0; i < group.Packages.Count; i++)
            {
                int reportCount = 0;
                IProgress<ExtractionProgress>? progress = i == 0 ? new CallbackProgress(_ =>
                {
                    if (reportCount++ == 0)
                        Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(10), token), "Independent groups did not overlap in extraction.");
                }) : null;
                var request = new ExtractionRequest(streams[group.Packages[i].Index], "",
                    Destination: staging.GetDestination(overwrite: i > 0));
                await extractor.ExtractAsync(request, progress, token);
            }
            await staging.PublishAsync(token);
            return new ExtractionResult(1, 1);
        });
        Assert.All(results, result => Assert.Null(result.Error));
        Assert.Equal(expected, await File.ReadAllBytesAsync(Path.Combine(workspace.Root, overlay.OutputName, "data", "payload.bin")));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(workspace.Root, overlay.OutputName, "data", "base-only.txt")));
        var otherGroup = plan.Groups.Single(group => group.Packages.Count == 1);
        Assert.Equal(other, await File.ReadAllBytesAsync(Path.Combine(workspace.Root, otherGroup.OutputName, "data", "payload.bin")));
        Assert.Empty(Directory.GetDirectories(workspace.Root, "*.partial"));
    }

    [Fact]
    public async Task FailedUpdateDiscardsTheUnpublishedBaseAndDoesNotAffectOtherGroups()
    {
        using var workspace = new TestDirectory();
        using var pkg = CreatePackage(out _, VolumeType.pkg_ps4_app, 17, "01.00");
        var protectedPath = Directory.CreateDirectory(Path.Combine(workspace.Root, "existing")).FullName;
        await File.WriteAllTextAsync(Path.Combine(protectedPath, "marker.txt"), "keep");

        var results = await ParallelExtraction.RunAsync(new[] { "failed", "other" }, () => 2, async (name, token) =>
        {
            await using var staging = new LocalExtractionWorkspace(workspace.Root, name);
            if (name == "failed")
            {
                var extractor = new OpenOrbisPkgExtractor();
                await extractor.ExtractAsync(new ExtractionRequest(pkg, "", Destination: staging.GetDestination(false)), cancellationToken: token);
                using var invalidUpdate = new MemoryStream(new byte[0x1100]);
                await extractor.ExtractAsync(new ExtractionRequest(invalidUpdate, "", Destination: staging.GetDestination(true)), cancellationToken: token);
            }
            await staging.PublishAsync(token);
            return new ExtractionResult(0, 0);
        });
        Assert.IsType<PkgExtractionException>(results.Single(result => result.Item == "failed").Error);
        Assert.Null(results.Single(result => result.Item == "other").Error);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "failed")));
        Assert.Empty(Directory.GetDirectories(workspace.Root, "*.partial"));
        Assert.True(Directory.Exists(Path.Combine(workspace.Root, "other")));
        Assert.Throws<PkgExtractionException>(() => new LocalExtractionWorkspace(workspace.Root, "existing"));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(protectedPath, "marker.txt")));
    }

    [Fact]
    public async Task ReadsEmbeddedCoverFromANonSeekablePackageWithoutReadingThePayload()
    {
        using var package = CreatePackage(out _);
        using var source = new ForwardOnlyStream(package.ToArray());
        var preview = await new OpenOrbisPkgMetadataReader().ReadPreviewAsync(source);
        Assert.Equal(PackageKind.Update, preview.Metadata.Kind);
        Assert.Equal("BBBB00000", preview.Metadata.TitleId);
        Assert.Equal(8, preview.ArtworkWidth);
        Assert.Equal(Artwork, preview.Artwork);
        Assert.InRange(source.BytesRead, 1, 1024 * 1024);
    }

    [Fact]
    public async Task LargeFileReadsTheNextChunkWhileTheCurrentWriteIsPending()
    {
        using var generated = CreatePackage(out var payload, payloadLength: 9 * 1024 * 1024 + 37);
        var probe = new CopyOverlapProbe();
        using var package = new ReadProbeStream(generated.ToArray(), probe);
        var destination = new MemoryExtractionDestination(probe);

        await new OpenOrbisPkgExtractor().ExtractAsync(new ExtractionRequest(package, "", Destination: destination));

        Assert.True(probe.ReadWhileWriting.Task.IsCompletedSuccessfully);
        Assert.Equal(payload, destination.Payload);
    }

    private static readonly byte[] Artwork = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAA00lEQVR4AQHIADf/APjTeFUim2Qim3Mim4Iim5Eim6Aim/jTeABGK6X403hkK6VzK6WCK6WRK6X403ivK6UARjSvVTSv+NN4czSvgjSv+NN4oDSvrzSvAEY9uVU9uWQ9ufjTePjTeJE9uaA9ua89uQBGRsNVRsNkRsP403j403iRRsOgRsOvRsMARk/NVU/N+NN4c0/Ngk/N+NN4oE/Nr0/NAEZY1/jTeGRY13NY14JY15FY1/jTeK9Y1wD403hVYeFkYeFzYeGCYeGRYeGgYeH403jQyWsRvyJ4ZAAAAABJRU5ErkJggg==");

    private static MemoryStream CreatePackage(out byte[] payload, VolumeType kind = VolumeType.pkg_ps4_patch,
        int seed = 17, string version = "01.02", string contentId = "AA0000-BBBB00000_00-ZZZZZZZZZZZZZZZZ",
        int payloadLength = 2 * 1024 * 1024 + 17)
    {
        payload = new byte[payloadLength];
        new Random(seed).NextBytes(payload);
        var root = new FSDir();
        var system = new FSDir { name = "sce_sys", Parent = root };
        var data = new FSDir { name = "data", Parent = root };
        root.Dirs.Add(system);
        root.Dirs.Add(data);
        var sfo = new ParamSfo();
        sfo["TITLE"] = new Utf8Value("TITLE", "Synthetic package", 128);
        sfo["CONTENT_ID"] = new Utf8Value("CONTENT_ID", contentId, 48);
        sfo["TITLE_ID"] = new Utf8Value("TITLE_ID", contentId.Split('-')[1].Split('_')[0], 16);
        sfo["APP_VER"] = new Utf8Value("APP_VER", version, 12);
        sfo["CATEGORY"] = new Utf8Value("CATEGORY", kind == VolumeType.pkg_ps4_app ? "gd" : "gp", 4);
        AddFile(system, "param.sfo", sfo.Serialize());
        AddFile(system, "icon0.png", Artwork);
        AddFile(data, "empty.bin", Array.Empty<byte>());
        AddFile(data, "payload.bin", payload);
        if (kind == VolumeType.pkg_ps4_app)
            AddFile(data, "base-only.txt", "keep"u8.ToArray());
        var builder = new PkgBuilder(new PkgProperties
        {
            VolumeType = kind,
            ContentId = contentId,
            Passcode = new string('0', 32),
            TimeStamp = new DateTime(2026, 1, 1),
            RootDir = root,
        });
        using var package = new MemoryStream();
        builder.Write(package, _ => { });
        return new ShortReadStream(package.ToArray());
    }

    private sealed class ForwardOnlyStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public long BytesRead => base.Position;
    }

    private sealed class CopyOverlapProbe
    {
        public volatile bool WaitingForRead;
        public TaskCompletionSource ReadWhileWriting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class ReadProbeStream(byte[] bytes, CopyOverlapProbe probe) : MemoryStream(bytes)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (probe.WaitingForRead) probe.ReadWhileWriting.TrySetResult();
            return base.Read(buffer, offset, count);
        }
    }

    private sealed class WriteProbeStream(CopyOverlapProbe probe) : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!probe.ReadWhileWriting.Task.IsCompleted)
            {
                probe.WaitingForRead = true;
                await probe.ReadWhileWriting.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            base.Write(buffer.Span);
        }
    }

    private sealed class MemoryExtractionDestination(CopyOverlapProbe probe) : IExtractionDestination
    {
        public byte[]? Payload { get; private set; }
        public Task<Stream> CreateTemporaryFileAsync(string relativePath, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(relativePath == "data/payload.bin" ? new WriteProbeStream(probe) : new MemoryStream());

        public Task CommitAsync(Stream temporaryFile, string relativePath, CancellationToken cancellationToken = default)
        {
            if (relativePath == "data/payload.bin") Payload = ((MemoryStream)temporaryFile).ToArray();
            temporaryFile.Dispose();
            return Task.CompletedTask;
        }

        public Task DeleteTemporaryFileAsync(Stream temporaryFile, CancellationToken cancellationToken = default)
        {
            temporaryFile.Dispose();
            return Task.CompletedTask;
        }
    }

    private static void AddFile(FSDir folder, string name, byte[] bytes) =>
        folder.Files.Add(new FSFile(s => s.Write(bytes), name, bytes.Length) { Parent = folder });

    private sealed class ShortReadStream(byte[] data) : MemoryStream(data, writable: true)
    {
        private int ReadLimit => Position < 0x80000 ? 3 : 8191;
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, ReadLimit));
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, ReadLimit)]);
    }

    private sealed class CallbackProgress(Action<ExtractionProgress> callback) : IProgress<ExtractionProgress>
    {
        public void Report(ExtractionProgress value) => callback(value);
    }

    private sealed class TestDirectory : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("orbisextract-test-").FullName;
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
