using System.Buffers.Binary;
using LibOrbisPkg.PFS;
using LibOrbisPkg.Util;

namespace OrbisExtract.Tests;

public sealed class PfscReaderTests
{
    [Theory]
    [InlineData(2L * 1024 * 1024 * 1024)]
    [InlineData(4L * 1024 * 1024 * 1024)]
    public void ReadsAcrossLargeOffsetsWithoutAllocatingPayload(long boundary)
    {
        using var image = new VirtualPfsc(boundary + VirtualPfsc.BlockSize);
        using var reader = new PFSCReader(image, image.Length);
        var actual = new byte[17];
        reader.Read(boundary - 4, actual, 2, 13);
        Assert.Equal(new byte[] { 0, 0, 252, 253, 254, 255, 0, 1, 2, 3, 4, 5, 6, 7, 8, 0, 0 }, actual);
        Assert.Equal(1, image.PayloadReads);
        reader.Read(reader.Length, actual, 0, 0);
        Assert.Equal(1, image.PayloadReads);
    }

    [Fact]
    public void RejectsSectorMapOutsideTheStoredImage()
    {
        using var image = new VirtualPfsc(VirtualPfsc.BlockSize);
        Assert.Throws<InvalidDataException>(() => new PFSCReader(image, image.Length - 1));
    }

    private sealed class VirtualPfsc : IMemoryReader
    {
        public const int BlockSize = 65536;
        private const int HeaderSize = 48;
        private readonly byte[] metadata;
        public long Length { get; }
        public int PayloadReads { get; private set; }

        public VirtualPfsc(long uncompressedSize)
        {
            var blocks = checked((int)(uncompressedSize / BlockSize));
            metadata = new byte[HeaderSize + (blocks + 1) * sizeof(long)];
            Length = metadata.Length + uncompressedSize;
            BinaryPrimitives.WriteInt32LittleEndian(metadata, PFSCReader.Magic);
            BinaryPrimitives.WriteInt32LittleEndian(metadata.AsSpan(12), BlockSize);
            BinaryPrimitives.WriteInt64LittleEndian(metadata.AsSpan(16), BlockSize);
            BinaryPrimitives.WriteInt64LittleEndian(metadata.AsSpan(24), HeaderSize);
            BinaryPrimitives.WriteInt64LittleEndian(metadata.AsSpan(32), metadata.Length);
            BinaryPrimitives.WriteInt64LittleEndian(metadata.AsSpan(40), uncompressedSize);
            for (var block = 0; block <= blocks; block++)
                BinaryPrimitives.WriteInt64LittleEndian(
                    metadata.AsSpan(HeaderSize + block * sizeof(long)), metadata.Length + (long)block * BlockSize);
        }

        public void Read(long position, byte[] buffer, int offset, int count)
        {
            Assert.InRange(position, 0, Length - count);
            if (position < metadata.Length)
            {
                metadata.AsSpan((int)position, count).CopyTo(buffer.AsSpan(offset));
                return;
            }
            PayloadReads++;
            for (var i = 0; i < count; i++)
                buffer[offset + i] = (byte)((position - metadata.Length + i) & 255);
        }

        public void Dispose() { }
    }
}
