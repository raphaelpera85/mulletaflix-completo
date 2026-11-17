using System;
using MediaBrowser.Controller.MediaEncoding;
using Xunit;

namespace Jellyfin.MediaEncoding.Tests.Transcoding;

public class FfmpegLogSanitizerTests
{
    [Fact]
    public void Redact_RemovesUrlCredentialsQueriesAndSensitiveJsonValues()
    {
        const string input = """{"Path":"https://user:pass@media.example/video.mkv?ApiKey=api-secret\u0026Part=2","OpenToken":"token-secret"}""";

        var result = FfmpegLogSanitizer.Redact(input);

        Assert.Contains("https://[REDACTED]@media.example/video.mkv?[REDACTED]", result, StringComparison.Ordinal);
        Assert.DoesNotContain("user", result, StringComparison.Ordinal);
        Assert.DoesNotContain("pass", result, StringComparison.Ordinal);
        Assert.DoesNotContain("api-secret", result, StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_RemovesSensitiveHeadersInFfmpegOutput()
    {
        const string input = "Authorization: Bearer auth-secret X-Emby-Token: media-secret";

        var result = FfmpegLogSanitizer.Redact(input);

        Assert.DoesNotContain("auth-secret", result, StringComparison.Ordinal);
        Assert.DoesNotContain("media-secret", result, StringComparison.Ordinal);
    }
}
