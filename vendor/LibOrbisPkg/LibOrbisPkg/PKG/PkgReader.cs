using System;
using LibOrbisPkg.Util;
using System.IO;
using System.Threading;

namespace LibOrbisPkg.PKG
{
  public class PkgReader : Util.ReaderBase
  {
    public PkgReader(System.IO.Stream s) : base(true, s)
    {
    }

    private Pkg ReadStructure(CancellationToken cancellationToken, bool metadataPrefix = false)
    {
      if (s.Length < 0x1100)
        throw new InvalidDataException("Truncated PKG header");
      var header = ReadHeader();
      if (header.entry_count > ushort.MaxValue ||
          (ulong)header.entry_table_offset + (ulong)header.entry_count * Pkg.PKG_TABLE_ENTRY_SIZE > (ulong)s.Length)
        throw new InvalidDataException("Invalid PKG entry table");
      s.Position = 0xFE0;
      var headerDigest = s.ReadBytes(32);
      var headerSignature = s.ReadBytes(256);
      s.Position = header.entry_table_offset;
      var metasEntry = new MetasEntry();
      for (var i = 0; i < header.entry_count; i++)
      {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = MetaEntry.Read(s);
        if (!metadataPrefix && (ulong)entry.DataOffset + entry.DataSize > (ulong)s.Length)
          throw new InvalidDataException($"PKG entry outside the file: {entry.id}");
        metasEntry.Metas.Add(entry);
      }
      return new Pkg
      {
        Header = header,
        HeaderDigest = headerDigest,
        HeaderSignature = headerSignature,
        Metas = metasEntry,
      };
    }

    public Pkg ReadMetadata(CancellationToken cancellationToken = default, bool metadataPrefix = false)
    {
      var pkg = ReadStructure(cancellationToken, metadataPrefix);
      foreach (var entry in pkg.Metas.Metas)
      {
        if (entry.id != EntryId.PARAM_SFO) continue;
        if ((ulong)entry.DataOffset + entry.DataSize > (ulong)s.Length)
          throw new InvalidDataException("PKG metadata outside the readable prefix");
        try { pkg.ParamSfo = ReadParamSfo(entry); }
        catch (InvalidDataException) { /* Header identity remains available. */ }
        catch (EndOfStreamException) { }
        break;
      }
      return pkg;
    }

    private SfoEntry ReadParamSfo(MetaEntry entry)
    {
      using var data = new SubStream(s, entry.DataOffset, entry.DataSize);
      return new SfoEntry(SFO.ParamSfo.FromStream(data)) { meta = entry };
    }

    public Pkg ReadPkg(bool readOptionalEntries = true, CancellationToken cancellationToken = default)
    {
      var pkg = ReadStructure(cancellationToken);
      foreach (var entry in pkg.Metas.Metas)
      {
        cancellationToken.ThrowIfCancellationRequested();
        if (!readOptionalEntries && entry.id != EntryId.ENTRY_KEYS && entry.id != EntryId.IMAGE_KEY)
          continue;
        switch (entry.id)
        {
          case EntryId.METAS:
            pkg.Metas.meta = entry;
            break;
          case EntryId.ENTRY_NAMES:
            pkg.EntryNames = NameTableEntry.Read(entry, s);
            break;
          case EntryId.PARAM_SFO:
            pkg.ParamSfo = ReadParamSfo(entry);
            break;
          case EntryId.ENTRY_KEYS:
            if (entry.DataSize < 2048)
              throw new InvalidDataException("Truncated PKG key table");
            pkg.EntryKeys = KeysEntry.Read(entry, s);
            pkg.EntryKeys.meta = entry;
            break;
          case EntryId.IMAGE_KEY:
            if (entry.DataSize != 256)
              throw new InvalidDataException("Invalid PKG image key size");
            s.Position = entry.DataOffset;
            pkg.ImageKey = new GenericEntry(EntryId.IMAGE_KEY)
            {
              FileData = s.ReadBytes((int)entry.DataSize),
              meta = entry
            };
            break;
          case EntryId.GENERAL_DIGESTS:
            s.Position = entry.DataOffset;
            pkg.GeneralDigests = GeneralDigestsEntry.Read(s);
            pkg.GeneralDigests.meta = entry;
            break;
          case EntryId.DIGESTS:
            s.Position = entry.DataOffset;
            pkg.Digests = new GenericEntry(EntryId.DIGESTS)
            {
              FileData = s.ReadBytes((int)entry.DataSize),
              meta = entry,
            };
            break;
          case EntryId.LICENSE_DAT:
            try
            {
              var licenseDatBytes = new byte[entry.DataSize];
              s.Position = entry.DataOffset;
              s.Read(licenseDatBytes, 0, (int)entry.DataSize);
              using (var ms = new System.IO.MemoryStream(Entry.Decrypt(licenseDatBytes, pkg, entry)))
              {
                pkg.LicenseDat = new Rif.LicenseDatReader(ms).Read();
                pkg.LicenseDat.meta = entry;
              }
            }
            catch (Exception) { }
            break;
        }
      }
      return pkg;
    }

    public Header ReadHeader()
    {
      var hdr = new Header();
      s.Position = 0x00;
      hdr.CNTMagic = s.ReadASCIINullTerminated(4);
      if (hdr.CNTMagic != Pkg.MAGIC)
        throw new Exception("Invalid CNT header");
      s.Position = 0x04;
      hdr.flags = (PKGFlags)UInt();
      s.Position = 0x08;
      hdr.unk_0x08 = UInt();
      s.Position = 0x0C;
      hdr.unk_0x0C = UInt(); /* 0xF */
      s.Position = 0x10;
      hdr.entry_count = UInt();
      s.Position = 0x14;
      hdr.sc_entry_count = UShort();
      s.Position = 0x16;
      hdr.entry_count_2 = UShort(); /* same as entry_count */
      s.Position = 0x18;
      hdr.entry_table_offset = UInt();
      s.Position = 0x1C;
      hdr.main_ent_data_size = UInt();
      s.Position = 0x20;
      hdr.body_offset = ULong();
      s.Position = 0x28;
      hdr.body_size = ULong();
      s.Position = 0x40;
      hdr.content_id = s.ReadASCIINullTerminated(Pkg.PKG_CONTENT_ID_SIZE); // Length = PKG_CONTENT_ID_SIZE
      s.Position = 0x70;
      hdr.drm_type = (DrmType)UInt();
      s.Position = 0x74;
      hdr.content_type = (ContentType)UInt();
      s.Position = 0x78;
      hdr.content_flags = (ContentFlags)UInt();
      s.Position = 0x7C;
      hdr.promote_size = UInt();
      s.Position = 0x80;
      hdr.version_date = UInt();
      s.Position = 0x84;
      hdr.version_hash = UInt();
      s.Position = 0x88;
      hdr.unk_0x88 = UInt(); /* for delta patches only? */
      s.Position = 0x8C;
      hdr.unk_0x8C = UInt(); /* for delta patches only? */
      s.Position = 0x90;
      hdr.unk_0x90 = UInt(); /* for delta patches only? */
      s.Position = 0x94;
      hdr.unk_0x94 = UInt(); /* for delta patches only? */
      s.Position = 0x98;
      hdr.iro_tag = (IROTag)UInt();
      s.Position = 0x9C;
      hdr.ekc_version = UInt(); /* drm type version */
      s.Position = 0x100;
      hdr.sc_entries1_hash = ReadBytes(Pkg.HASH_SIZE);
      s.Position = 0x120;
      hdr.sc_entries2_hash = ReadBytes(Pkg.HASH_SIZE);
      s.Position = 0x140;
      hdr.digest_table_hash = ReadBytes(Pkg.HASH_SIZE);
      s.Position = 0x160;
      hdr.body_digest = ReadBytes(Pkg.HASH_SIZE);

      // TODO: i think these fields are actually members of element of container array
      s.Position = 0x400;
      hdr.unk_0x400 = UInt();
      s.Position = 0x404;
      hdr.pfs_image_count = UInt();
      s.Position = 0x408;
      hdr.pfs_flags = ULong();
      s.Position = 0x410;
      hdr.pfs_image_offset = ULong();
      s.Position = 0x418;
      hdr.pfs_image_size = ULong();
      s.Position = 0x420;
      hdr.mount_image_offset = ULong();
      s.Position = 0x428;
      hdr.mount_image_size = ULong();
      s.Position = 0x430;
      hdr.package_size = ULong();
      s.Position = 0x438;
      hdr.pfs_signed_size = UInt();
      s.Position = 0x43C;
      hdr.pfs_cache_size = UInt();
      s.Position = 0x440;
      hdr.pfs_image_digest = ReadBytes(Pkg.HASH_SIZE);
      s.Position = 0x460;
      hdr.pfs_signed_digest = ReadBytes(Pkg.HASH_SIZE);
      s.Position = 0x480;
      hdr.pfs_split_size_nth_0 = ULong();
      s.Position = 0x488;
      hdr.pfs_split_size_nth_1 = ULong();
      return hdr;
    }
  }
}
