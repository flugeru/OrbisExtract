using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OrbisExtract.Core;

public sealed record OnlineArtwork(
    byte[] Image, int Width, string Title, string Author, string License, Uri? LicenseUri, Uri SourceUri);

public sealed class CommonsArtworkSearch
{
    private const int MaxImageBytes = 3 * 1024 * 1024;
    private static readonly HttpClient DefaultClient = new(new HttpClientHandler { AllowAutoRedirect = false });
    private static readonly string[] ArtworkTerms = ["cover", "box art", "key art", "artwork", "wallpaper", "screenshot", "screen shot", "gameplay"];
    private readonly HttpClient client;
    private readonly Dictionary<string, (OnlineArtwork? Artwork, DateTimeOffset Expiry)> cache = new(StringComparer.OrdinalIgnoreCase);

    public CommonsArtworkSearch(HttpClient? client = null) => this.client = client ?? DefaultClient;

    public async Task<OnlineArtwork?> FindAsync(string gameTitle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (gameTitle.Length > 160) return null;
        var title = Normalize(gameTitle);
        if (title.Length < 3 || title is "titulo nao informado" or "unknown title") return null;

        lock (cache)
        {
            if (cache.TryGetValue(title, out var saved) && saved.Expiry > DateTimeOffset.UtcNow)
                return saved.Artwork;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        var token = timeout.Token;
        try
        {
            // Debounce rapid changes in the selected package before sending a query.
            await Task.Delay(350, token).ConfigureAwait(false);
            var result = await SearchAsync(title, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            lock (cache)
            {
                if (cache.Count >= 12 && !cache.ContainsKey(title))
                    cache.Remove(cache.MinBy(entry => entry.Value.Expiry).Key);
                cache[title] = (result, DateTimeOffset.UtcNow.AddMinutes(result is null ? 5 : 60));
            }
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (HttpRequestException) { return null; }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
    }

    private async Task<OnlineArtwork?> SearchAsync(string title, CancellationToken token)
    {
        var query = Uri.EscapeDataString($"\"{title}\" (cover OR artwork OR screenshot OR gameplay OR wallpaper)");
        var uri = new Uri("https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrnamespace=6&gsrlimit=10" +
            $"&gsrsearch={query}&prop=imageinfo&iiprop=url%7Csize%7Cmime%7Cextmetadata&iiurlwidth=512" +
            "&iiextmetadatafilter=Artist%7CLicenseShortName%7CLicenseUrl&format=json&formatversion=2");
        var json = await FetchAsync(uri, 128 * 1024, ["application/json"], token).ConfigureAwait(false);
        if (json is null) return null;
        using var document = JsonDocument.Parse(json);
        var pages = Property(Property(document.RootElement, "query"), "pages");
        if (pages.ValueKind != JsonValueKind.Array) return null;
        var candidates = pages.EnumerateArray().Select(page => Match(page, title)).OfType<Candidate>()
            .OrderByDescending(candidate => candidate.Score).Take(2);

        foreach (var candidate in candidates)
        {
            var image = await FetchAsync(candidate.Thumbnail, MaxImageBytes, ["image/jpeg", "image/png"], token).ConfigureAwait(false);
            if (image is { Length: >= 8 } && IsSupportedImage(image))
                return new OnlineArtwork(image, candidate.Width, candidate.Title, candidate.Author, candidate.License,
                    candidate.LicenseUri, candidate.Source);
        }
        return null;
    }

    private async Task<byte[]?> FetchAsync(Uri uri, int byteLimit, string[] mediaTypes, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("OrbisExtract/1.0.5 (+https://github.com/flugeru/OrbisExtract)");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > byteLimit ||
            !mediaTypes.Contains(response.Content.Headers.ContentType?.MediaType, StringComparer.OrdinalIgnoreCase))
            return null;
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            int count = await input.ReadAsync(buffer, token).ConfigureAwait(false);
            if (count == 0) return output.ToArray();
            if (output.Length + count > byteLimit) return null;
            output.Write(buffer, 0, count);
        }
    }

    private static Candidate? Match(JsonElement page, string gameTitle)
    {
        var filename = Text(page, "title");
        if (filename is null || !filename.StartsWith("File:", StringComparison.OrdinalIgnoreCase)) return null;
        var baseName = Path.GetFileNameWithoutExtension(filename[5..]);
        var normalized = Normalize(baseName);
        // Require both a title match and an artwork marker. Generic photos,
        // logos, or hardware with the same name are weak matches.
        if (!normalized.StartsWith(gameTitle + " ", StringComparison.Ordinal) &&
            !normalized.StartsWith("screenshot of " + gameTitle + " ", StringComparison.Ordinal) &&
            !normalized.Equals("screenshot of " + gameTitle, StringComparison.Ordinal))
            return null;
        var words = $" {normalized} ";
        int kind = Array.FindIndex(ArtworkTerms, term => words.Contains($" {term} ", StringComparison.Ordinal));
        if (kind < 0 || words.Contains(" logo ", StringComparison.Ordinal)) return null;

        var images = Property(page, "imageinfo");
        if (images.ValueKind != JsonValueKind.Array || images.GetArrayLength() == 0) return null;
        var image = images[0];
        var thumbnail = HttpsUri(Text(image, "thumburl"));
        var source = HttpsUri(Text(image, "descriptionurl"));
        if (thumbnail is null || thumbnail.Host is not ("upload.wikimedia.org" or "thumb.wikimedia.org") ||
            !thumbnail.AbsolutePath.StartsWith("/wikipedia/commons/", StringComparison.Ordinal) ||
            source is null || source.Host != "commons.wikimedia.org" ||
            !source.AbsolutePath.StartsWith("/wiki/File:", StringComparison.OrdinalIgnoreCase))
            return null;
        var width = Number(image, "thumbwidth");
        var height = Number(image, "thumbheight");
        if (width is < 200 or > 1920 || height is < 110 or > 1920) return null;

        var data = Property(image, "extmetadata");
        var license = Text(Property(data, "LicenseShortName"), "value");
        var licenseUri = HttpsUri(Text(Property(data, "LicenseUrl"), "value"));
        var authorHtml = Text(Property(data, "Artist"), "value") ?? "";
        var author = WebUtility.HtmlDecode(Regex.Replace(authorHtml, "<[^>]*>", " ", RegexOptions.CultureInvariant))
            .Trim();
        if (author.Length > 320) return null;
        if (license == "Public domain")
        {
            if (!IsCreativeCommonsLicense(licenseUri)) licenseUri = null;
        }
        else if (license is null || !IsCreativeCommonsLicense(licenseUri) ||
            (author.Length == 0 && licenseUri!.AbsolutePath.StartsWith("/licenses/", StringComparison.Ordinal)))
            return null;

        var clean = new UriBuilder(thumbnail) { Query = "" }.Uri;
        // Prefer cover art, then well-sized landscape artwork and screenshots.
        int score = (ArtworkTerms.Length - kind) * 10 + (width >= height ? 3 : 0);
        return new Candidate(clean, source, baseName, author, license, licenseUri, width, score);
    }

    private static bool IsCreativeCommonsLicense(Uri? uri) => uri?.Host == "creativecommons.org" &&
        (uri.AbsolutePath.StartsWith("/licenses/by/", StringComparison.Ordinal) ||
         uri.AbsolutePath.StartsWith("/licenses/by-sa/", StringComparison.Ordinal) ||
         uri.AbsolutePath.StartsWith("/publicdomain/zero/", StringComparison.Ordinal) ||
         uri.AbsolutePath.StartsWith("/publicdomain/mark/", StringComparison.Ordinal));

    private static bool IsSupportedImage(byte[] image) =>
        image.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
        (image[0] == 255 && image[1] == 216 && image[2] == 255);

    private static Uri? HttpsUri(string? raw)
    {
        if (raw is null) return null;
        if (raw.StartsWith("//", StringComparison.Ordinal)) raw = "https:" + raw;
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0) return null;
        if (uri.Scheme == Uri.UriSchemeHttp && uri.Host == "creativecommons.org")
            uri = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
        return uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort ? uri : null;
    }

    private static string Normalize(string input)
    {
        var result = new StringBuilder();
        foreach (char part in input.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(part) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(part)) result.Append(char.ToLowerInvariant(part));
            else if (result.Length > 0 && result[^1] != ' ') result.Append(' ');
        }
        return result.ToString().TrimEnd();
    }

    private static JsonElement Property(JsonElement node, string name) =>
        node.ValueKind == JsonValueKind.Object && node.TryGetProperty(name, out var value) ? value : default;

    private static string? Text(JsonElement node, string name) =>
        Property(node, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static int Number(JsonElement node, string name) =>
        Property(node, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt32(out var number) ? number : 0;

    private sealed record Candidate(Uri Thumbnail, Uri Source, string Title, string Author, string License, Uri? LicenseUri, int Width, int Score);
}
