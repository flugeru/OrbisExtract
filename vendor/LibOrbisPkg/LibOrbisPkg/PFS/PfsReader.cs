using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// Class allowing parallel readonly access to a PFS archive
  /// </summary>
  public class PfsReader
  {
    /// <summary>
    /// Represents a file or directory in a PFS image.
    /// </summary>
    public abstract class Node
    {
      public Dir parent;
      public string name;
      public long offset;
      public long size;
      public long compressed_size;
      public uint ino;
      public string FullName => parent != null ? parent.FullName + "/" + name : name;
    }
    /// <summary>
    /// Represents a directory in a PFS image.
    /// </summary>
    public class Dir : Node
    {
      public List<Node> children = new List<Node>();
      public Node Get(string name)
        => children.Where(x => x.name == name).FirstOrDefault();
      public Node GetPath(string name)
      {
        var breadcrumbs = name.Split('/');
        Node n = this;
        var bc = 0;
        while(n != null && bc < breadcrumbs.Length)
        {
          n = (n as Dir)?.Get(breadcrumbs[bc]);
          bc++;
        }
        if(bc < breadcrumbs.Length)
        {
          return null;
        }
        return n;
      }
      public IEnumerable<File> GetAllFiles()
      {
        foreach(var n in children)
        {
          if (n is File f) yield return f;
          if (n is Dir d)
            foreach (var x in d.GetAllFiles())
              yield return x;
        }
      }
    }
    /// <summary>
    /// Represents a file in a PFS image.
    /// </summary>
    public class File : Node
    {
      public InodeFlags flags;
      public int blockSize;
      public int[] blocks;
      private IMemoryReader reader;
      public File(IMemoryReader r) { reader = r; }
      public IMemoryReader GetView()
      {
        if (blocks != null)
          return new ChunkedMemoryReader(reader, blockSize, blocks);
        return new MemoryAccessor(reader, offset);
      }
      public void Save(string path, bool decompress = false)
      {
        var buf = new byte[blockSize];
        using (var file = System.IO.File.OpenWrite(path))
        {
          var sz = size;
          file.SetLength(sz);
          long pos = 0;
          var reader = GetView();
          if (decompress && flags.HasFlag(InodeFlags.compressed))
          {
            sz = compressed_size;
            reader = new PFSCReader(reader);
          }
          while (sz > 0)
          {
            var toRead = (int)Math.Min(sz, buf.Length);
            reader.Read(pos, buf, 0, toRead);
            file.Write(buf, 0, toRead);
            pos += toRead;
            sz -= toRead;
          }
        }
      }
    }

    // Private state for the PfsReader class
    private IMemoryReader reader;
    private PfsHeader hdr;
    private Inode[] dinodes;
    private Dir root;
    private Dir uroot;
    private byte[] sectorBuf;
    private Stream sectorStream;
    private readonly long? imageLength;

    public PfsReader(MemoryMappedViewAccessor r, ulong pfs_flags = 0, byte[] ekpfs = null, byte[] tweak = null, byte[] data = null)
    : this(new MemoryMappedViewAccessor_(r), pfs_flags, ekpfs, tweak, data, imageLength: r.Capacity)
    { }
    public PfsReader(IMemoryReader r, ulong pfs_flags = 0, byte[] ekpfs = null, byte[] tweak = null, byte[] data = null, ReadTimings timings = null, long? imageLength = null)
    {
      reader = r;
      this.imageLength = imageLength;
      var buf = new byte[0x400];
      reader.Read(0, buf, 0, 0x400);

      using (var ms = new MemoryStream(buf))
      {
        hdr = PfsHeader.ReadFromStream(ms);
      }
      int dinodeSize;
      Func<Stream, Inode> dinodeReader;
      if (hdr.Mode.HasFlag(PfsMode.Signed))
      {
        dinodeReader = DinodeS32.ReadFromStream;
        dinodeSize = 0x2C8;
      }
      else
      {
        dinodeReader = DinodeD32.ReadFromStream;
        dinodeSize = 0xA8;
      }
      if (hdr.BlockSize < 4096 || hdr.BlockSize > 1024 * 1024 ||
          (hdr.BlockSize & (hdr.BlockSize - 1)) != 0)
        throw new InvalidDataException("Invalid PFS block size");

      var maxPerSector = hdr.BlockSize / dinodeSize;
      var dinodeStartPos = (long)hdr.BlockSize * (1 + hdr.InodeBlockSig.IndirectBlocks.Count(b => b > 0));
      if (hdr.DinodeCount <= 0 || hdr.DinodeCount > int.MaxValue || hdr.DinodeBlockCount <= 0 ||
          hdr.DinodeBlockCount > (long.MaxValue - dinodeStartPos) / hdr.BlockSize ||
          hdr.DinodeCount > hdr.DinodeBlockCount * maxPerSector ||
          dinodeStartPos + hdr.DinodeBlockCount * hdr.BlockSize > imageLength)
        throw new InvalidDataException("Invalid PFS inode table");
      dinodes = new Inode[hdr.DinodeCount];

      if (hdr.Mode.HasFlag(PfsMode.Encrypted))
      {
        const int XtsSectorSize = 0x1000;
        uint XtsStartSector = hdr.BlockSize / XtsSectorSize;
        if (ekpfs == null && (tweak == null || data == null))
          throw new ArgumentException("PFS image is encrypted but no decryption key was provided");
        if (ekpfs != null)
        {
          var (tweakKey, dataKey) = Crypto.PfsGenEncKey(ekpfs, hdr.Seed, (pfs_flags & 0x2000000000000000UL) != 0);
          reader = new XtsDecryptReader(reader, dataKey, tweakKey, XtsStartSector, XtsSectorSize, timings);
        }
        else
        {
          reader = new XtsDecryptReader(reader, data, tweak, XtsStartSector, XtsSectorSize, timings);
        }
      }
      var total = 0;

      sectorBuf = new byte[hdr.BlockSize];
      sectorStream = new MemoryStream(sectorBuf);
      // skip over indirect blocks, block signatures are never checked anyway
      for (long i = 0; i < hdr.DinodeBlockCount && total < hdr.DinodeCount; i++)
      {
        var position = dinodeStartPos + (hdr.BlockSize * i);
        reader.Read(position, sectorBuf, 0, sectorBuf.Length);
        sectorStream.Position = 0;
        for (var j = 0; j < maxPerSector && total < hdr.DinodeCount; j++)
          dinodes[total++] = dinodeReader(sectorStream);
      }
      root = LoadDir(0, null, "", new HashSet<uint>(), 0);
      uroot = root.Get("uroot") as Dir;
      if (uroot == null)
        throw new Exception("Invalid PFS image (no uroot)");
      uroot.name = "uroot";
    }

    public PfsHeader Header => hdr;

    public File GetFile(string fullPath)
    {
      return uroot.GetPath(fullPath) as File;
    }

    public IEnumerable<File> GetAllFiles()
    {
      return uroot.GetAllFiles();
    }

    public Dir GetURoot()
    {
      return uroot;
    }

    public Dir GetSuperRoot()
    {
      return root;
    }

    private Dir LoadDir(uint dinode, Dir parent, string name, HashSet<uint> visited, int depth)
    {
      // 100M blocks is enough for a 6TB file.
      const int MAX_BLOCKS = 100_000_000;
      if (depth > 256 || !visited.Add(dinode))
        throw new InvalidDataException("Invalid PFS directory tree (repeated inode or excessive depth)");
      var ret = new Dir() { name = name, parent = parent };
      var ino = GetInode(dinode);
      var postLoad = new List<PfsDirent>();
      var blocks = (int)ino.Blocks;
      if (blocks < 1 || ino.StartBlock < 1 || ino.StartBlock > MAX_BLOCKS || blocks > MAX_BLOCKS ||
          ((long)ino.StartBlock + blocks) * hdr.BlockSize > imageLength)
      {
        throw new Exception($"Inode {dinode} is corrupt. ");
      }
      foreach (var x in Enumerable.Range(ino.StartBlock, blocks))
      {
        var position = hdr.BlockSize * x;
        reader.Read(position, sectorBuf, 0, sectorBuf.Length);
        sectorStream.Position = 0;
        while (sectorStream.Length - sectorStream.Position >= 16)
        {
          var dirent = PfsDirent.ReadFromStream(sectorStream);
          if (dirent.EntSize == 0) break;
          switch (dirent.Type)
          {
            case DirentType.File:
              ret.children.Add(LoadFile(dirent.InodeNumber, ret, dirent.Name));
              break;
            case DirentType.Directory:
              postLoad.Add(dirent);
              break;
            case DirentType.Dot:
              break;
            case DirentType.DotDot:
              break;
            default:
              break;
          }
        }
      }
      foreach (var p in postLoad)
      {
        ret.children.Add(LoadDir(p.InodeNumber, ret, p.Name, visited, depth + 1));
      }
      return ret;
    }

    private Inode GetInode(uint number)
    {
      if (number >= dinodes.Length || dinodes[number] == null)
        throw new InvalidDataException($"Missing PFS inode {number}");
      return dinodes[number];
    }

    private File LoadFile(uint dinode, Dir parent, string name)
    {
      var inode = GetInode(dinode);
      var offset = (long)inode.StartBlock * hdr.BlockSize;
      if (inode.Size < 0 || offset < 0 || inode.Size > (long)inode.Blocks * hdr.BlockSize ||
          (inode.Size > 0 && inode.StartBlock == 0))
        throw new InvalidDataException($"Invalid PFS file extent for inode {dinode}");
      int[] blocks = null;
      if(inode.Blocks > 1 && inode.DirectBlocks[1] != -1)
      {
        if (!hdr.Mode.HasFlag(PfsMode.Signed))
        {
          throw new Exception("Unsigned PFS images probably shouldn't have noncontiguous blocks");
        }
        if (inode.Blocks > 100_000_000 || inode.Blocks > imageLength / hdr.BlockSize)
          throw new InvalidDataException($"Invalid PFS block count for inode {dinode}");
        blocks = new int[inode.Blocks];
        var remainingBlocks = (long)inode.Blocks;
        var sigsPerBlock = hdr.BlockSize / 36;
        for (int i = 0; i < 12 && i < remainingBlocks; i++)
        {
          blocks[i] = inode.DirectBlocks[i];
        }

        var bufferedReader = new BufferedMemoryReader(reader, 0x10000);
        remainingBlocks -= 12;
        long blockIndexOffset = 12;
        for (int i = 0; i < remainingBlocks && i < sigsPerBlock; i++)
        {
          bufferedReader.Read(inode.IndirectBlocks[0] * hdr.BlockSize + (i * 36) + 32, out blocks[i + blockIndexOffset]);
        }
        remainingBlocks -= sigsPerBlock;
        blockIndexOffset += sigsPerBlock;
        for (int j = 0; j * sigsPerBlock < remainingBlocks; j++)
        {
          bufferedReader.Read(inode.IndirectBlocks[1] * hdr.BlockSize + (j * 36) + 32, out int indirectBlockOffset);
          for (int i = 0; i < sigsPerBlock && i + (j * sigsPerBlock) < remainingBlocks; i++)
          {
            bufferedReader.Read(indirectBlockOffset * hdr.BlockSize + (i * 36) + 32, out blocks[i + blockIndexOffset]);
          }
          blockIndexOffset += sigsPerBlock;
        }
        bool contiguous = blocks[0] == inode.StartBlock;
        for(int i = 0; i < blocks.Length; i++)
        {
          if (blocks[i] <= 0 || ((long)blocks[i] + 1) * hdr.BlockSize > imageLength)
            throw new InvalidDataException($"Invalid PFS data block for inode {dinode}");
          if (i > 0 && blocks[i - 1] + 1L != blocks[i])
            contiguous = false;
        }
        if (contiguous)
          blocks = null;
      }
      if (blocks == null && imageLength is long length && (offset > length || inode.Size > length - offset))
        throw new InvalidDataException($"PFS file outside the image (inode {dinode})");
      return new File(reader)
      {
        name = name,
        parent = parent,
        offset = offset,
        size = inode.Size,
        compressed_size = inode.SizeCompressed,
        ino = dinode,
        blocks = blocks,
        flags = inode.Flags,
        blockSize = (int)hdr.BlockSize
      };
    }
  }
}
