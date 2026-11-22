using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrbisExtract.App;

/// <summary>Local, bounded history of PKG extraction attempts. No package contents are stored.</summary>
internal sealed record ExtractionHistoryEntry(
    DateTimeOffset StartedAt, string PackageName, string Destination,
    string Result, long Files, long Bytes, long ElapsedSeconds,
    List<string> Events);

internal static class ExtractionHistory
{
    private static readonly object Sync = new();
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OrbisExtract", "extraction-history.json");

    public static List<ExtractionHistoryEntry> Load()
    {
        lock (Sync)
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                return JsonSerializer.Deserialize(File.ReadAllText(FilePath), ExtractionHistoryJsonContext.Default.ListExtractionHistoryEntry) ?? new();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new();
            }
        }
    }

    public static void Add(ExtractionHistoryEntry entry)
    {
        lock (Sync)
        {
            try
            {
                var entries = Load();
                entries.Insert(0, entry);
                if (entries.Count > 100) entries.RemoveRange(100, entries.Count - 100);
                var path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var temporary = path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(entries, ExtractionHistoryJsonContext.Default.ListExtractionHistoryEntry));
                File.Move(temporary, path, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Extraction results must not be affected by a history write failure.
            }
        }
    }
}

[JsonSerializable(typeof(List<ExtractionHistoryEntry>))]
internal sealed partial class ExtractionHistoryJsonContext : JsonSerializerContext;
