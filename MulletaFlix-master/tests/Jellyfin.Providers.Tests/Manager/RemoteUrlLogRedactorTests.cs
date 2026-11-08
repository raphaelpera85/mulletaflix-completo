using Xunit;

namespace MulletaFlix.Providers.Tests.Manager;

public sealed class RemoteUrlLogRedactorTests
{
    [Theory]
    [InlineData("Configured M3U: https://user:pass@provider.example/get.php?username=alice&password=secret", "Configured M3U: [REDACTED_URL]")]
    [InlineData("Channel: https://provider.example/alice/secret/123.ts", "Channel: [REDACTED_URL]")]
    [InlineData("EPG sync started", "EPG sync started")]
    public void Redact_RemovesRemoteUrlsFromLogText(string value, string expected)
        => Assert.Equal(expected, MediaBrowser.Providers.Helpers.RemoteUrlLogRedactor.Redact(value));
}
