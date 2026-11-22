using OrbisExtract.App.Updates;

namespace OrbisExtract.Tests;

public class AndroidUpdatePolicyTests
{
    [Fact]
    public void PlayDoesNotFallBackToGithubWhenMissingOrUnpublished()
    {
        var github = new AndroidUpdateChannel { Published = true, Version = "9.0.0" };
        Assert.Null(AndroidUpdatePolicy.SelectChannel(true, null, github, "9.0.0", "https://github.com/"));
        Assert.Null(AndroidUpdatePolicy.SelectChannel(true, new AndroidUpdateChannel { Published = false }, github, "9.0.0", null));
    }

    [Fact]
    public void ChannelsHaveIndependentVersionsAndLegacyGithubStillWorks()
    {
        var play = new AndroidUpdateChannel { Published = true, Version = "1.0.5" };
        var github = new AndroidUpdateChannel { Published = true, Version = "1.0.6" };
        Assert.Same(play, AndroidUpdatePolicy.SelectChannel(true, play, github, "1.0.6", null));
        Assert.Same(github, AndroidUpdatePolicy.SelectChannel(false, play, github, "1.0.6", null));
        Assert.Equal("1.0.4", AndroidUpdatePolicy.SelectChannel(false, null, null, "1.0.4", "https://github.com/")!.Version);
        Assert.Null(AndroidUpdatePolicy.SelectChannel(false, play, new AndroidUpdateChannel { Published = false }, "9.0.0", null));
    }

    [Theory]
    [InlineData("1.0.5", 1051001, "1.0.5", 1051000, true)]
    [InlineData("1.0.6", 1050999, "1.0.5", 1051000, false)]
    [InlineData("1.0.5", 1051000, "1.0.5", 1051000, false)]
    [InlineData("1.0.6", 0, "1.0.5", 0, true)]
    public void VersionCodeControlsAndroidUpgrade(string latest, long code, string installed, long current, bool expected)
        => Assert.Equal(expected, AndroidUpdatePolicy.IsNewer(latest, code, installed, current));

    [Theory]
    [InlineData("https://play.google.com/store/apps/details?id=com.tatsuya.orbisextract", true, true)]
    [InlineData("https://github.com/flugeru/OrbisExtract/releases/tag/1.0.5", false, true)]
    [InlineData("https://github.com/flugeru/OrbisExtract/releases/tag/1.0.5", true, false)]
    [InlineData("https://evil.test/flugeru/OrbisExtract/releases/tag/1.0.5", false, false)]
    [InlineData("https://github.com/flugeru/OtherApp/releases/latest", false, false)]
    [InlineData("https://play.google.com/store/apps/details?id=other.app", true, false)]
    [InlineData("http://github.com/flugeru/OrbisExtract/releases/latest", false, false)]
    [InlineData("https://github.com:8443/flugeru/OrbisExtract/releases/latest", false, false)]
    public void RejectsWrongStoreAndUntrustedDestinations(string url, bool play, bool expected)
        => Assert.Equal(expected, AndroidUpdatePolicy.IsAllowedUrl(url, play));
}
