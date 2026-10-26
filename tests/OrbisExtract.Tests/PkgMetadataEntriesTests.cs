using LibOrbisPkg.PKG;
using System.Security.Cryptography;
using Xunit.Abstractions;
using System.IO.MemoryMappedFiles;
using LibOrbisPkg.PFS;

namespace OrbisExtract.Tests;

public sealed class PkgMetadataEntriesTests
{
    private readonly ITestOutputHelper output;
    public PkgMetadataEntriesTests(ITestOutputHelper output) => this.output = output;
    // Set both paths to compare the *original PKG entry bytes* with a known
    // working full dump. No game payload is copied by this test.
    [Fact]
    public void PkgTableSystemFilesMatchReferenceDump()
    {
        var pkgPath = Environment.GetEnvironmentVariable("ORBISEXTRACT_TEST_PKG");
        var reference = Environment.GetEnvironmentVariable("ORBISEXTRACT_TEST_DUMP");
        if (string.IsNullOrEmpty(pkgPath) || string.IsNullOrEmpty(reference)) return;

        using var pkgFile = File.OpenRead(pkgPath);
        var pkg = new PkgReader(pkgFile).ReadPkg();
        var passcode = Environment.GetEnvironmentVariable("ORBISEXTRACT_TEST_PASSCODE") ?? new string('0', 32);
        Assert.True(pkg.CheckPasscode(passcode), "Passcode de teste inválido");
        var matched = 0;
        foreach (var meta in pkg.Metas.Metas)
        {
            if (!EntryNames.IdToName.TryGetValue(meta.id, out var name) || name.StartsWith('.'))
                continue;
            var referencePath = Path.Combine(reference, "sce_sys", name.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(referencePath)) continue;

            var readSize = meta.Encrypted ? ((long)meta.DataSize + 15) & ~15L : meta.DataSize;
            Assert.True((ulong)meta.DataOffset + (ulong)readSize <= (ulong)pkgFile.Length, name);
            using var expected = File.OpenRead(referencePath);
            Assert.Equal((long)meta.DataSize, expected.Length);
            pkgFile.Position = meta.DataOffset;
            using var hash = SHA256.Create();
            var buffer = new byte[128 * 1024];
            using var actual = new MemoryStream();
            long remaining = readSize;
            while (remaining > 0)
            {
                var n = pkgFile.Read(buffer, 0, (int)Math.Min(remaining, buffer.Length));
                Assert.True(n > 0, $"PKG truncado: {name}");
                actual.Write(buffer, 0, n);
                remaining -= n;
            }
            actual.Position = 0;
            if (meta.Encrypted)
            {
                var decrypted = meta.KeyIndex == 3
                    ? Entry.Decrypt(actual.ToArray(), pkg, meta)
                    : Entry.Decrypt(actual.ToArray(), pkg.Header.content_id, passcode, meta);
                actual.SetLength(0);
                actual.Write(decrypted, 0, checked((int)meta.DataSize));
                actual.Position = 0;
            }
            Assert.True(hash.ComputeHash(expected).SequenceEqual(hash.ComputeHash(actual)),
                $"Bytes divergentes: {name} (ID {meta.id}, flags {meta.Flags1:X8}/{meta.Flags2:X8})");
            matched++;
        }
        output.WriteLine($"Tabela PKG: {matched} arquivos idênticos ao PS4 PKG Tool");
        Assert.True(matched >= 20, $"Apenas {matched} arquivos da tabela PKG corresponderam ao dump.");
    }

    [Fact]
    public void PackageFilesystemInventoryMatchesReferencePaths()
    {
        var pkgPath = Environment.GetEnvironmentVariable("ORBISEXTRACT_TEST_PKG");
        var reference = Environment.GetEnvironmentVariable("ORBISEXTRACT_TEST_DUMP");
        if (string.IsNullOrEmpty(pkgPath) || string.IsNullOrEmpty(reference)) return;

        using var pkgFile = File.OpenRead(pkgPath);
        var pkg = new PkgReader(pkgFile).ReadPkg();
        using var map = MemoryMappedFile.CreateFromFile(pkgFile, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
        using var view = map.CreateViewAccessor((long)pkg.Header.pfs_image_offset, (long)pkg.Header.pfs_image_size, MemoryMappedFileAccess.Read);
        var outer = new PfsReader(view, pkg.Header.pfs_flags, pkg.GetEkpfs());
        var inner = new PfsReader(new PFSCReader(outer.GetFile("pfs_image.dat").GetView()));
        var outerNames = outer.GetAllFiles().Select(f => f.FullName).ToArray();
        output.WriteLine("PFS externo: " + string.Join(", ", outerNames.Take(40)));
        var missing = new List<string>();
        var payloadCount = 0;
        var inventory = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in inner.GetAllFiles())
        {
            payloadCount++;
            var name = f.FullName.TrimStart('/');
            if (name.StartsWith("uroot/", StringComparison.OrdinalIgnoreCase)) name = name[6..];
            if (name.StartsWith("Image0/", StringComparison.OrdinalIgnoreCase)) name = name[7..];
            inventory.Add(name, f.size);
            if (!File.Exists(Path.Combine(reference, name.Replace('/', Path.DirectorySeparatorChar))))
                missing.Add(name);
        }
        output.WriteLine($"PFS interno: {payloadCount} arquivos");
        Assert.Empty(missing);
        var missingMetadata = pkg.Metas.Metas
            .Where(m => EntryNames.IdToName.TryGetValue(m.id, out var name) && !name.StartsWith('.'))
            .Select(m => EntryNames.IdToName[m.id])
            .Where(name => !File.Exists(Path.Combine(reference, "sce_sys", name.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Empty(missingMetadata);
        foreach (var meta in pkg.Metas.Metas)
        {
            if (!EntryNames.IdToName.TryGetValue(meta.id, out var name) || name.StartsWith('.')) continue;
            inventory.Add("sce_sys/" + name, meta.DataSize);
        }
        var referenceFiles = Directory.GetFiles(reference, "*", SearchOption.AllDirectories);
        Assert.Equal(referenceFiles.Length, inventory.Count);
        foreach (var file in referenceFiles)
        {
            var name = Path.GetRelativePath(reference, file).Replace('\\', '/');
            Assert.True(inventory.TryGetValue(name, out var size), $"Ausente no plano: {name}");
            Assert.Equal(new FileInfo(file).Length, size);
        }
    }
}
