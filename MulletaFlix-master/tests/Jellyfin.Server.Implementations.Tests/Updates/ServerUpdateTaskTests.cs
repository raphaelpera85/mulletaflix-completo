using System;
using Emby.Server.Implementations.ScheduledTasks.Tasks;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Updates;

public sealed class ServerUpdateTaskTests
{
    [Fact]
    public void TryParseGitHubRelease_RequiresAndReturnsSha256Digest()
    {
        var digest = "sha256:" + new string('a', 64);
        var json = $$"""{"tag_name":"v12.1.35","assets":[{"name":"server.zip","browser_download_url":"https://example.test/server.zip","digest":"{{digest}}"}]}""";

        Assert.True(ServerUpdateTask.TryParseGitHubRelease(json, out var version, out var archiveUrl, out var checksum));
        Assert.Equal(new Version(12, 1, 35), version);
        Assert.Equal("https://example.test/server.zip", archiveUrl);
        Assert.Equal(digest, checksum);
    }

    [Fact]
    public void TryParseGitHubRelease_RejectsMissingDigest()
    {
        const string json = "{\"tag_name\":\"v12.1.35\",\"assets\":[{\"name\":\"server.zip\",\"browser_download_url\":\"https://example.test/server.zip\"}]}";

        Assert.False(ServerUpdateTask.TryParseGitHubRelease(json, out _, out _, out _));
    }

    [Fact]
    public void TryParseGitHubRelease_RejectsHttpArchiveUrl()
    {
        const string json = "{\"tag_name\":\"v12.1.35\",\"assets\":[{\"name\":\"server.zip\",\"browser_download_url\":\"http://example.test/server.zip\",\"digest\":\"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}]}";

        Assert.False(ServerUpdateTask.TryParseGitHubRelease(json, out _, out _, out _));
    }

    [Fact]
    public void TryParseCustomManifest_RequiresSha256Digest()
    {
        var digest = new string('b', 64);
        var json = $$"""{"Version":"12.1.36","ArchiveUrl":"https://example.test/server.zip","Checksum":"{{digest}}"}""";

        Assert.True(ServerUpdateTask.TryParseCustomManifest(json, out var version, out var archiveUrl, out var checksum));
        Assert.Equal(new Version(12, 1, 36), version);
        Assert.Equal("https://example.test/server.zip", archiveUrl);
        Assert.Equal(digest, checksum);
    }

    [Fact]
    public void TryParseCustomManifest_RejectsHttpArchiveUrl()
    {
        const string json = "{\"Version\":\"12.1.36\",\"ArchiveUrl\":\"http://example.test/server.zip\",\"Checksum\":\"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}";

        Assert.False(ServerUpdateTask.TryParseCustomManifest(json, out _, out _, out _));
    }
}
