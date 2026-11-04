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

    /// <summary>
    /// Creates an AES-XTS-128 stream.
    /// Reads will decrypt data.
    /// </summary>
    public XtsDecryptReader(
      IMemoryReader r,
      byte[] dataKey,
      byte[] tweakKey, uint startSector = 16, uint sectorSize = 0x1000)
    {
      cryptStartSector = startSector;
      this.sectorSize = sectorSize;
      this.dataKey = dataKey;
      this.tweakKey = tweakKey;
      reader = r;
    }

    public static unsafe void DecryptSector(
      Ctx context,
      byte[] sector,
      ulong sectorNum)
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
        tweakEncryptor.TransformBlock(tweak, 0, 16, encryptedTweak, 0);
        ulong low = BinaryPrimitives.ReadUInt64LittleEndian(encryptedTweak);
        ulong high = BinaryPrimitives.ReadUInt64LittleEndian(encryptedTweak.AsSpan(8));
        fixed (byte* sectorBytes = sector)
        fixed (byte* tweakBytes = context.sectorTweaks)
        {
          for (int plaintextOffset = 0; plaintextOffset < sector.Length; plaintextOffset += 16)
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
        // One native AES call per sector, not one interop call per 16-byte block.
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
      reader.Read(currentSector * sectorSize, sectorBuf, 0, (int)sectorSize);
      if (currentSector >= cryptStartSector)
        DecryptSector(ctx, sectorBuf, (ulong)currentSector);
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
