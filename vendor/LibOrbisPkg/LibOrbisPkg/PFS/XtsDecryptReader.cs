using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using LibOrbisPkg.Util;

namespace LibOrbisPkg.PFS
{
  /// <summary>
  /// Provides XTS decryption on an IMemoryReader
  /// </summary>
  public class XtsDecryptReader : IMemoryReader
  {
    private byte[] dataKey;
    private byte[] tweakKey;
    /// <summary>
    /// Size of each XEX sector
    /// </summary>
    private uint sectorSize;
    /// <summary>
    /// Sector at and after which the encryption is active
    /// </summary>
    private uint cryptStartSector;
    private IMemoryReader reader;
    private static byte[] zeroes = new byte[16];
    private readonly ReadTimings timings;

    /// <summary>
    /// Creates an AES-XTS-128 stream.
    /// Reads will decrypt data.
    /// </summary>
    public XtsDecryptReader(
      IMemoryReader r,
      byte[] dataKey,
      byte[] tweakKey, uint startSector = 16, uint sectorSize = 0x1000, ReadTimings timings = null)
    {
      cryptStartSector = startSector;
      this.sectorSize = sectorSize;
      this.dataKey = dataKey;
      this.tweakKey = tweakKey;
      reader = r;
      this.timings = timings;
    }

    public static unsafe void DecryptSector(
      Ctx context,
      byte[] sector,
      ulong sectorNum)
      => DecryptSectors(context, sector, sectorNum, sector.Length);

    private static unsafe void DecryptSectors(Ctx context, byte[] sector, ulong sectorNum, int blockSize)
    {
      byte[] tweak = context.tweak,
        encryptedTweak = context.encryptedTweak,
        xor = context.xor;

      if (sector.Length % 16 != 0)
        throw new ArgumentException("XTS sector must contain complete AES blocks.", nameof(sector));
      if (xor.Length < sector.Length)
        xor = context.xor = new byte[sector.Length];
      if (context.sectorTweaks.Length < sector.Length)
        context.sectorTweaks = new byte[sector.Length];

      // Reset tweak to sector number
      Buffer.BlockCopy(BitConverter.GetBytes(sectorNum), 0, tweak, 0, 8);
      Buffer.BlockCopy(zeroes, 0, tweak, 8, 8);
      // Reuse native crypto handles across sectors in this read operation.
      var tweakEncryptor = context.TweakEncryptor;
      var decryptor = context.Decryptor;
      {
        fixed (byte* sectorBytes = sector)
        fixed (byte* tweakBytes = context.sectorTweaks)
        {
          for (int blockOffset = 0; blockOffset < sector.Length; blockOffset += blockSize)
          {
            BinaryPrimitives.WriteUInt64LittleEndian(tweak, sectorNum + (ulong)(blockOffset / blockSize));
            tweakEncryptor.TransformBlock(tweak, 0, 16, encryptedTweak, 0);
            ulong low = BinaryPrimitives.ReadUInt64LittleEndian(encryptedTweak);
            ulong high = BinaryPrimitives.ReadUInt64LittleEndian(encryptedTweak.AsSpan(8));
            for (int plaintextOffset = blockOffset; plaintextOffset < blockOffset + blockSize; plaintextOffset += 16)
            {
              // XTS tweaks are little-endian; preserve that on other architectures.
              ulong first = BitConverter.IsLittleEndian ? low : BinaryPrimitives.ReverseEndianness(low);
              ulong second = BitConverter.IsLittleEndian ? high : BinaryPrimitives.ReverseEndianness(high);
              var data = (ulong*)(sectorBytes + plaintextOffset);
              var saved = (ulong*)(tweakBytes + plaintextOffset);
              data[0] ^= first;
              data[1] ^= second;
              saved[0] = first;
              saved[1] = second;
              // Multiply the 128-bit tweak by x in GF(2^128).
              ulong carry = low >> 63;
              low = (low << 1) ^ ((high >> 63) != 0 ? 0x87UL : 0UL);
              high = (high << 1) | carry;
            }
          }
        }
        // ECB permits batching independent XTS sectors in one native data call.
        decryptor.TransformBlock(sector, 0, sector.Length, xor, 0);
        fixed (byte* sectorBytes = sector)
        fixed (byte* xorBytes = xor)
        fixed (byte* tweakBytes = context.sectorTweaks)
        {
          for (int i = 0; i < sector.Length; i += sizeof(ulong))
            *(ulong*)(sectorBytes + i) = *(ulong*)(xorBytes + i) ^ *(ulong*)(tweakBytes + i);
        }
      }
    }

    public class Ctx : IDisposable
    {
      public SymmetricAlgorithm cipher;
      public SymmetricAlgorithm tweakCipher;
      public byte[] tweak;
      public byte[] xor;
      public byte[] encryptedTweak;
      public byte[] sectorTweaks = Array.Empty<byte>();
      private ICryptoTransform decryptor;
      private ICryptoTransform tweakEncryptor;
      internal ICryptoTransform Decryptor => decryptor ??= cipher.CreateDecryptor();
      internal ICryptoTransform TweakEncryptor => tweakEncryptor ??= tweakCipher.CreateEncryptor();

      public void Dispose()
      {
        decryptor?.Dispose();
        tweakEncryptor?.Dispose();
        cipher.Dispose();
        tweakCipher.Dispose();
      }
    }

    /// <summary>
    /// Precondition: activeSector is set
    /// Postconditions:
    /// - sectorOffset is reset to 0
    /// - sectorBuf[] is filled with decrypted sector
    /// - position is updated
    /// </summary>
    private void ReadSectorBuffer(Ctx ctx, int currentSector, byte[] sectorBuf)
    {
      reader.Read((long)currentSector * sectorSize, sectorBuf, 0, sectorBuf.Length);
      if (currentSector >= cryptStartSector)
      {
        long started = timings == null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
        DecryptSectors(ctx, sectorBuf, (ulong)currentSector, (int)sectorSize);
        if (timings != null)
          timings.DecryptionTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
      }
    }

    private Ctx MakeCtx()
    {
      var cipher = Aes.Create();
      cipher.Mode = CipherMode.ECB;
      cipher.KeySize = 128;
      if (dataKey != null) cipher.Key = dataKey;
      cipher.Padding = PaddingMode.None;
      cipher.BlockSize = 128;
      var tweakCipher = Aes.Create();
      tweakCipher.Mode = CipherMode.ECB;
      tweakCipher.KeySize = 128;
      if (tweakKey != null) tweakCipher.Key = tweakKey;
      tweakCipher.Padding = PaddingMode.None;
      tweakCipher.BlockSize = 128;
      return new Ctx {
        cipher = cipher,
        tweakCipher = tweakCipher,
        xor = new byte[16],
        encryptedTweak = new byte[16],
        tweak = new byte[16]
      };
    }

    public void Read(long position, byte[] buffer, int offset, int count)
    {
      using var ctx = MakeCtx();
      // Batch only complete encrypted sectors. Partial reads retain sector
      // boundaries and never read beyond the caller's requested full sectors.
      if (position % sectorSize == 0 && position / sectorSize >= cryptStartSector && count >= sectorSize)
      {
        int batchSize = (int)Math.Min(64 * 1024 / sectorSize, count / sectorSize) * (int)sectorSize;
        if (batchSize > 0)
        {
          var batch = new byte[batchSize];
          while (count >= batchSize)
          {
            ReadSectorBuffer(ctx, checked((int)(position / sectorSize)), batch);
            Buffer.BlockCopy(batch, 0, buffer, offset, batchSize);
            position += batchSize;
            offset += batchSize;
            count -= batchSize;
          }
          if (count == 0) return;
        }
      }
      var sectorBuf = new byte[sectorSize];
      var currentSector = (int)(position / sectorSize);
      var offsetIntoSector = (int)(position - (sectorSize * currentSector));
      ReadSectorBuffer(ctx, currentSector, sectorBuf);
      int totalRead = 0;
      while (count > 0)
      {
        if (offsetIntoSector >= sectorSize)
        {
          currentSector++;
          ReadSectorBuffer(ctx, currentSector, sectorBuf);
          offsetIntoSector = 0;
        }
        int bufferedRead = Math.Min((int)sectorSize - offsetIntoSector, count);
        Buffer.BlockCopy(sectorBuf, offsetIntoSector, buffer, offset, bufferedRead);
        count -= bufferedRead;
        offset += bufferedRead;
        totalRead += bufferedRead;
        offsetIntoSector += bufferedRead;
        position += bufferedRead;
      }
    }

    public void Dispose()
    {
      // We don't own the IMemoryReader, so do nothing.
    }
  }
}
