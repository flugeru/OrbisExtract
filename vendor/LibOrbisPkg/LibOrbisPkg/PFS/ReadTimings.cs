namespace LibOrbisPkg.PFS
{
  // Per-extraction counters; readers are used sequentially, not concurrently.
  public sealed class ReadTimings
  {
    public long PackageReadTicks;
    public long DecryptionTicks;
    public long DecompressionTicks;
  }
}
