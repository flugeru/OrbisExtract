using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OrbisExtract.Core;

namespace OrbisExtract.Tests;

public sealed class OnlineArtworkTests
{
    [Fact]
    public async Task DownloadsOnlyAMatchingLicensedCommonsImageAndCachesIt()
    {
        using var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.Host == "commons.wikimedia.org")
            {
                Assert.Contains("\"super game\"", Uri.UnescapeDataString(request.RequestUri.Query));
                Assert.Contains("OrbisExtract/", request.Headers.UserAgent.ToString());
                return JsonResult(
                    Entry("File:Other Super Game artwork.png", "thumb.wikimedia.org"),
                    Entry("File:Super Game cover.png", "example.org"),
                    Entry("File:Super Game cover 2.png", "thumb.wikimedia.org", "https://creativecommons.org/licenses/by-nc/4.0/"),
                    Entry("File:Super Game screenshot.png", "thumb.wikimedia.org"));
            }

            Assert.Equal("thumb.wikimedia.org", request.RequestUri.Host);
            Assert.Empty(request.RequestUri.Query);
            var result = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([137, 80, 78, 71, 13, 10, 26, 10])
            };
            result.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return result;
        });
        using var client = new HttpClient(handler);
        var provider = new CommonsArtworkSearch(client);
        var match = Assert.IsType<OnlineArtwork>(await provider.FindAsync("Super Game"));
        Assert.Equal("Super Game screenshot", match.Title);
        Assert.Equal("Studio & Contributors", match.Author);
        Assert.Equal("CC BY 4.0", match.License);
        Assert.Equal("creativecommons.org", match.LicenseUri?.Host);
        Assert.Equal("commons.wikimedia.org", match.SourceUri.Host);
        Assert.Same(match, await provider.FindAsync("Súper Game"));
        Assert.Equal(2, handler.Requests);
    }

    [Fact]
    public async Task DismissesMisleadingMatchesWithoutDownloadingAnImage()
    {
        using var handler = new StubHandler(_ => JsonResult(Entry("File:Air traffic control screenshot.png", "thumb.wikimedia.org")));
        using var client = new HttpClient(handler);
        Assert.Null(await new CommonsArtworkSearch(client).FindAsync("Control"));
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task CancelsAnObsoletePreviewBeforeSendingTheLookup()
    {
        using var handler = new StubHandler(_ => throw new InvalidOperationException("No HTTP request expected"));
        using var client = new HttpClient(handler);
        using var stop = new CancellationTokenSource();
        var pending = new CommonsArtworkSearch(client).FindAsync("Super Game", stop.Token);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, handler.Requests);
    }

    private static object Entry(string title, string assetHost, string license = "https://creativecommons.org/licenses/by/4.0/") =>
        new
        {
            title,
            imageinfo = new[]
            {
                new
                {
                    thumburl = $"https://{assetHost}/wikipedia/commons/thumb/sample.png?utm_source=commons.wikimedia.org",
                    descriptionurl = "https://commons.wikimedia.org/wiki/File:Sample.png",
                    thumbwidth = 512,
                    thumbheight = 288,
                    extmetadata = new
                    {
                        Artist = new { value = "<span>Studio &amp; Contributors</span>" },
                        LicenseShortName = new { value = "CC BY 4.0" },
                        LicenseUrl = new { value = license },
                    }
                }
            }
        };

    private static HttpResponseMessage JsonResult(params object[] pages) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { query = new { pages } }), Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(respond(request));
        }
    }
}
