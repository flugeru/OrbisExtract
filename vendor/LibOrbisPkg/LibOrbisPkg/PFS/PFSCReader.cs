using System;
using System.Runtime.InteropServices;
using System.Buffers.Binary;
using System.Buffers;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.IO.Compression;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// This wraps a Memory mapped file view of a PFSC file so that you can access it
  /// as though it were uncompressed.
  /// </summary>
  public class PFSCReader : IMemoryReader
  {
    public const int Magic = 0x43534650;
    private IMemoryReader _accessor;
    private PFSCHdr hdr;
    private long[] sectorMap;
    public ReadTimings Timings { get; set; }

    /// <summary>
    /// Creates a PFSCReader
    /// </summary>
    /// <param name="va">A reader containing the PFSC file</param>
    /// <param name="containerLength">The stored size of the PFSC file, when known.</param>
    /// <exception cref="ArgumentException">Thrown when the accessor is not a view of a PFSC file.</exception>
    public PFSCReader(IMemoryReader va, long? containerLength = null)
    {
      const int HeaderSize = 0x30;
      if (containerLength < HeaderSize)
        throw new InvalidDataException("Truncated PFSC header");
      _accessor = va;
      _accessor.Read(0, out hdr);
      if (hdr.Magic != Magic)
        throw new ArgumentException("Not a PFSC file: missing PFSC magic");
      if (hdr.Unk4 != 0)
        throw new ArgumentException($"Not a PFSC file: unknown data at 0x4 (expected 0, got {hdr.Unk4})");
      //if (hdr.Unk8 != 6)
      //  throw new ArgumentException($"Not a PFSC file: unknown data at 0x8 (expected 6, got {hdr.Unk8})");
      if (hdr.BlockSz != hdr.BlockSz2 || hdr.BlockSz < 512 || hdr.BlockSz > 1024 * 1024 ||
          (hdr.BlockSz & (hdr.BlockSz - 1)) != 0 || hdr.DataLength <= 0 ||
          hdr.DataLength % hdr.BlockSz != 0)
        throw new InvalidDataException("Invalid PFSC block size or data length");

      var entries = hdr.DataLength / hdr.BlockSz + 1;
      var tableBytes = entries * sizeof(long);
      if (tableBytes > 128 * 1024 * 1024 || hdr.BlockOffsets < HeaderSize ||
          hdr.BlockOffsets > long.MaxValue - tableBytes ||
          (containerLength is long length && hdr.BlockOffsets > length - tableBytes))
        throw new InvalidDataException("PFSC sector table is outside the container or too large");

      sectorMap = new long[(int)entries];
      var buffer = new byte[(int)Math.Min(tableBytes, 64 * 1024)];
      long previous = hdr.BlockOffsets + tableBytes;
      for (int index = 0; index < sectorMap.Length;)
      {
        int count = Math.Min(buffer.Length / sizeof(long), sectorMap.Length - index);
        _accessor.Read(hdr.BlockOffsets + (long)index * sizeof(long), buffer, 0, count * sizeof(long));
        for (int i = 0; i < count; i++, index++)
        {
          long offset = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(i * sizeof(long)));
          if (offset < previous || (index > 0 && offset - previous < 2) || offset > containerLength)
            throw new InvalidDataException("Invalid PFSC sector offset");
          sectorMap[index] = previous = offset;
        }
      }
    }

    /// <summary>
    /// Creates a PFSCReader
    /// </summary>
    /// <param name="va">A ViewAccessor containing the PFSC file</param>
    /// <exception cref="ArgumentException">Thrown when the accessor is not a view of a PFSC file.</exception>
    public PFSCReader(MemoryMappedViewAccessor va) : this(new MemoryMappedViewAccessor_(va), va.Capacity)
    { }

    public int SectorSize => hdr.BlockSz;
    public long Length => hdr.DataLength;
    
    /// <summary>
    /// Reads the sector at the given index into the given byte array.
    /// </summary>
    /// <param name="idx">sector index (multiply by SectorSize to get the byte offset)</param>
    /// <param name="output">byte array where sector will be written</param>
    public void ReadSector(int idx, byte[] output)
    {
      if (idx < 0 || idx >= sectorMap.Length - 1)
        throw new ArgumentException("Invalid index", nameof(idx));

      var sectorOffset = sectorMap[idx];
      var sectorSize = sectorMap[idx + 1] - sectorOffset;

      if(sectorSize == hdr.BlockSz2)
      {
        // fast case: uncompressed sector
        _accessor.Read(sectorOffset, output, 0, hdr.BlockSz);
      }
      else if (sectorSize > hdr.BlockSz2)
      {
        Array.Clear(output, 0, hdr.BlockSz);
      }
      else
      {
        // slow case: compressed sector
        int compressedLength = (int)sectorSize - 2;
        var sectorBuf = ArrayPool<byte>.Shared.Rent(compressedLength);
        try
        {
          _accessor.Read(sectorOffset + 2, sectorBuf, 0, compressedLength);
          var timings = Timings;
          long started = timings == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
          using var bufStream = new MemoryStream(sectorBuf, 0, compressedLength, writable: false);
          using var ds = new DeflateStream(bufStream, CompressionMode.Decompress);
          // The rented output may contain data from a previous read.
          ds.ReadExactly(output, 0, hdr.BlockSz);
          if (timings != null)
            System.Threading.Interlocked.Add(ref timings.DecompressionTicks, System.Diagnostics.Stopwatch.GetTimestamp() - started);
        }
        finally { ArrayPool<byte>.Shared.Return(sectorBuf); }
      }
    }
    
    private void ValidateRange(long src, long count)
    {
      if (src < 0 || count < 0 || src > hdr.DataLength || count > hdr.DataLength - src)
        throw new ArgumentException("Attempt to read beyond end of file");
    }

    /// <summary>
    /// Read `count` bytes at location `src` into the writeable Stream `dest`
    /// </summary>
    /// <param name="src">Byte offset into PFSC</param>
    /// <param name="count">Number of bytes to read</param>
    /// <param name="dest">Output stream</param>
    public void Read(long src, long count, Stream dest)
    {
      ValidateRange(src, count);
      if (count == 0) return;
      var buffer = ArrayPool<byte>.Shared.Rent(256 * 1024);
      try
      {
        while (count > 0)
        {
          int read = (int)Math.Min(count, buffer.Length);
          Read(src, buffer, 0, read);
          dest.Write(buffer, 0, read);
          src += read;
          count -= read;
        }
      }
      finally { ArrayPool<byte>.Shared.Return(buffer); }
    }

    /// <summary>
    /// Read `count` bytes at location `src` into the byte array at offset `offset`
    /// </summary>
    /// <param name="src">Byte offset into PFSC</param>
    /// <param name="buffer">Output byte array</param>
    /// <param name="offset">Offset into byte array</param>
    /// <param name="count">Number of bytes to read</param>
    public void Read(long src, byte[] buffer, int offset, int count)
    {
      ValidateRange(src, count);
      _ = buffer.AsSpan(offset, count);
      byte[] scratch = null;
      try
      {
        while (count > 0)
        {
          int index = (int)(src / hdr.BlockSz);
          int withinSector = (int)(src % hdr.BlockSz);
          int bytes = Math.Min(count, hdr.BlockSz - withinSector);
          if (IsUncompressed(index))
          {
            int next = index + 1;
            while (bytes < count && next < sectorMap.Length - 1 && IsUncompressed(next))
            {
              bytes += Math.Min(count - bytes, hdr.BlockSz);
              next++;
            }
            _accessor.Read(sectorMap[index] + withinSector, buffer, offset, bytes);
          }
          else
          {
            scratch ??= ArrayPool<byte>.Shared.Rent(hdr.BlockSz);
            ReadSector(index, scratch);
            Buffer.BlockCopy(scratch, withinSector, buffer, offset, bytes);
          }
          src += bytes;
          offset += bytes;
          count -= bytes;
        }
      }
      finally { if (scratch != null) ArrayPool<byte>.Shared.Return(scratch); }
    }

    private bool IsUncompressed(int index) => sectorMap[index + 1] - sectorMap[index] == hdr.BlockSz;

    public void Dispose()
    {
      _accessor.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 0x30)]
    private struct PFSCHdr
    {
      public int Magic;
      public int Unk4;
      public int Unk8;
      public int BlockSz;
      public long BlockSz2;
      public long BlockOffsets;
      public ulong DataStart;
      public long DataLength;
    }
  }
}
