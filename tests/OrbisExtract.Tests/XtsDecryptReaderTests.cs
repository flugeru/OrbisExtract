using LibOrbisPkg.PFS;
using LibOrbisPkg.Util;

namespace OrbisExtract.Tests;

public class XtsDecryptReaderTests
{
    [Theory]
    [InlineData(4096)]
    [InlineData(16384)]
    public void BatchedDecryptionMatchesBlockTransformAcrossSectorsAndPartialReads(int sectorSize)
    {
        var dataKey = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var tweakKey = Enumerable.Range(16, 16).Select(i => (byte)i).ToArray();
        var plaintext = new byte[sectorSize * 40];
        new Random(42).NextBytes(plaintext);
        var ciphertext = (byte[])plaintext.Clone();
        var reference = new XtsBlockTransform(dataKey, tweakKey);
        for (int i = 1; i < 40; i++)
        {
            var sector = plaintext.AsSpan(i * sectorSize, sectorSize).ToArray();
            reference.EncryptSector(sector, (ulong)i);
            sector.CopyTo(ciphertext, i * sectorSize);
        }

        var timings = new ReadTimings();
        var source = new BytesReader(ciphertext);
        using var reader = new XtsDecryptReader(source, dataKey, tweakKey, 1, (uint)sectorSize, timings);
        foreach (var (position, count) in new[] { (0, plaintext.Length), (17, 9000), (sectorSize, sectorSize), (sectorSize + 2, sectorSize * 38 + 17), (sectorSize * 2 - 1, 33), (sectorSize * 3, sectorSize), (plaintext.Length, 0) })
        {
            var actual = new byte[count + 14];
            int callsBefore = source.ReadCalls;
            reader.Read(position, actual, 7, count);
            Assert.Equal(plaintext.AsSpan(position, count).ToArray(), actual.AsSpan(7, count).ToArray());
            Assert.All(actual.Take(7).Concat(actual.Skip(count + 7)), b => Assert.Equal((byte)0, b));
            if (position == sectorSize + 2)
                Assert.True(source.ReadCalls - callsBefore < count / sectorSize);
        }
        Assert.True(timings.DecryptionTicks > 0);
        Assert.Equal(0L, timings.PackageReadTicks);
        Assert.Equal(0L, timings.DecompressionTicks);
    }

    private sealed class BytesReader(byte[] bytes) : IMemoryReader
    {
        public int ReadCalls { get; private set; }
        public void Read(long pos, byte[] buf, int offset, int count)
        {
            ReadCalls++;
            Array.Copy(bytes, pos, buf, offset, count);
        }
        public void Dispose() { }
    }
}
