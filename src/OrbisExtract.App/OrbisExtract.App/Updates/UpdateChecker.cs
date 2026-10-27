using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrbisExtract.App.Localization;

namespace OrbisExtract.App.Updates;

public static class UpdateChecker
{
    private const string ManifestUrl = "https://raw.githubusercontent.com/flugeru/OrbisExtract/main/update.json";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(8) };

    // Set by the Android host. Unknown installers must not be treated as Play installs.
    public static Func<bool>? IsPlayInstall { get; set; }
    public static Action<string>? OpenAndroidUrl { get; set; }

    public static Func<string>? GetInstalledVersion { get; set; }
    public static string CurrentVersion => GetInstalledVersion?.Invoke() ?? (Assembly.GetEntryAssembly() ?? typeof(UpdateChecker).Assembly).GetName().Version?.ToString(3) ?? "1.0.3";

    public static async Task<UpdateResult?> CheckAsync(CancellationToken cancellationToken = default)
    {
        var manifestUri = new Uri(ManifestUrl + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var response = await Client.GetAsync(manifestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }, cancellationToken);
        if (manifest is null || !Version.TryParse(manifest.Version, out var latest) ||
            !Version.TryParse(CurrentVersion, out var installed))
            throw new InvalidOperationException(AppLanguage.Text("Manifesto de atualização inválido."));

        if (latest <= installed) return null;

        var url = OperatingSystem.IsAndroid()
            ? (IsPlayInstall?.Invoke() == true ? manifest.AndroidPlayUrl : manifest.AndroidGithubUrl)
            : OperatingSystem.IsWindows() ? manifest.WindowsUrl
            : OperatingSystem.IsLinux() ? manifest.LinuxUrl : null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(AppLanguage.Text("Link de atualização inválido ou indisponível para esta plataforma."));

        return new UpdateResult(manifest.Version!, uri.ToString(), manifest.ReleaseNotes ?? Array.Empty<string>(), manifest.ReleaseNotesEn);
    }

    private sealed class UpdateManifest
    {
        public string? Version { get; set; }
        public string? AndroidPlayUrl { get; set; }
        public string? AndroidGithubUrl { get; set; }
        public string? WindowsUrl { get; set; }
        public string? LinuxUrl { get; set; }
        public string[]? ReleaseNotes { get; set; }
        public string[]? ReleaseNotesEn { get; set; }
    }
}

public sealed record UpdateResult(string Version, string Url, IReadOnlyList<string> ReleaseNotes, IReadOnlyList<string>? ReleaseNotesEn)
{
    public IReadOnlyList<string> NotesForCurrentLanguage => AppLanguage.IsEnglish && ReleaseNotesEn is { Count: > 0 } ? ReleaseNotesEn : ReleaseNotes;
}
