using System;

namespace OrbisExtract.App.Updates;

public sealed class AndroidUpdateChannel
{
    public bool Published { get; set; }
    public string? Version { get; set; }
    public long VersionCode { get; set; }
    public string? Url { get; set; }
    public string[]? ReleaseNotes { get; set; }
    public string[]? ReleaseNotesEn { get; set; }
}

public static class AndroidUpdatePolicy
{
    public static AndroidUpdateChannel? SelectChannel(bool play, AndroidUpdateChannel? playChannel,
        AndroidUpdateChannel? githubChannel, string legacyVersion, string? legacyGithubUrl)
    {
        var channel = play ? playChannel : githubChannel ?? new AndroidUpdateChannel
        {
            Published = true, Version = legacyVersion, Url = legacyGithubUrl
        };
        return channel is { Published: true } ? channel : null;
    }

    public static bool IsNewer(string latestVersion, long latestCode, string installedVersion, long installedCode)
    {
        if (!Version.TryParse(latestVersion, out var latest) ||
            !Version.TryParse(installedVersion, out var installed))
            throw new InvalidOperationException("Invalid Android update version.");
        // Android installs are ordered by versionCode, including rebuilds with the same versionName.
        return latestCode > 0 && installedCode > 0 ? latestCode > installedCode : latest > installed;
    }

    public static bool IsAllowedUrl(string? value, bool play)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
            return false;
        return play
            ? uri.Host == "play.google.com" && uri.AbsolutePath == "/store/apps/details" &&
              uri.Query == "?id=com.tatsuya.orbisextract"
            : uri.Host == "github.com" && uri.AbsolutePath.StartsWith("/flugeru/OrbisExtract/releases/", StringComparison.Ordinal);
    }
}
