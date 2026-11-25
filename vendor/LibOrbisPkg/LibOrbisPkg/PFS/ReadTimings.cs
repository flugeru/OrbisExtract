namespace LibOrbisPkg.PFS
{
  // Per-extraction counters; readers increment them atomically across file workers.
  public sealed class ReadTimings
  {
    public long PackageReadTicks;
    public long DecryptionTicks;
    public long DecompressionTicks;
  }
}
