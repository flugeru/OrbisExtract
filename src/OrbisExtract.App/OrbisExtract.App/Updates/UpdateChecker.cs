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

    public static Func<long>? GetInstalledVersionCode { get; set; }
    public static Func<string>? GetInstalledVersion { get; set; }
    public static string CurrentVersion => GetInstalledVersion?.Invoke() ?? (Assembly.GetEntryAssembly() ?? typeof(UpdateChecker).Assembly).GetName().Version?.ToString(3) ?? "1.0.5";

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

        string? url;
        string version = manifest.Version!;
        var notes = manifest.ReleaseNotes;
        var notesEn = manifest.ReleaseNotesEn;
        if (OperatingSystem.IsAndroid())
        {
            bool play = IsPlayInstall?.Invoke() == true;
            var channel = AndroidUpdatePolicy.SelectChannel(play, manifest.AndroidPlay,
                manifest.AndroidGithub, manifest.Version!, manifest.AndroidGithubUrl);
            if (channel is null) return null;
            version = channel.Version ?? throw new InvalidOperationException("Missing channel version.");
            if (!AndroidUpdatePolicy.IsNewer(version, channel.VersionCode, CurrentVersion,
                GetInstalledVersionCode?.Invoke() ?? 0)) return null;
            url = channel.Url;
            // Each channel can be on a different release; do not show another channel's changelog.
            notes = channel.ReleaseNotes ?? (version == manifest.Version ? manifest.ReleaseNotes : Array.Empty<string>());
            notesEn = channel.ReleaseNotesEn ?? (version == manifest.Version ? manifest.ReleaseNotesEn : null);
            if (!AndroidUpdatePolicy.IsAllowedUrl(url, play))
                throw new InvalidOperationException("Invalid Android update destination.");
        }
        else
        {
            if (latest <= installed) return null;
            url = OperatingSystem.IsWindows() ? manifest.WindowsUrl
                : OperatingSystem.IsLinux() ? manifest.LinuxUrl : null;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(AppLanguage.Text("Link de atualização inválido ou indisponível para esta plataforma."));

        return new UpdateResult(version, uri.ToString(), notes ?? Array.Empty<string>(), notesEn);
    }

    private sealed class UpdateManifest
    {
        public AndroidUpdateChannel? AndroidPlay { get; set; }
        public AndroidUpdateChannel? AndroidGithub { get; set; }
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
