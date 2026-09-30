using MulletaFlix.LiveTv;
using Xunit;

namespace Jellyfin.LiveTv.Tests;

public class LiveTvUrlRedactorTests
{
    [Fact]
    public void Redact_RemovesUserInfoCredentials()
    {
        var redacted = LiveTvUrlRedactor.Redact("http://alice:s3cr3t@provider.example:8080/live/1234.ts");

        Assert.DoesNotContain("alice", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("s3cr3t", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider.example:8080", redacted, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_RemovesXtreamStyleCredentialsEmbeddedInPath()
    {
        // Padrão Xtream Codes: /<usuario>/<senha>/<stream id>. Não há forma
        // confiável de distinguir esses segmentos de um path comum, portanto
        // o path inteiro é redigido.
        var redacted = LiveTvUrlRedactor.Redact("http://provider.example:8080/alice/s3cr3t/1234.ts");

        Assert.DoesNotContain("alice", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("s3cr3t", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider.example:8080", redacted, System.StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_RemovesQueryStringEntirely()
    {
        var redacted = LiveTvUrlRedactor.Redact("https://provider.example/stream?username=alice&password=s3cr3t&api_key=abcd1234");

        Assert.DoesNotContain("alice", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("s3cr3t", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abcd1234", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("username", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider.example", redacted, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_KeepsSchemeAndHostForDiagnostics()
    {
        var redacted = LiveTvUrlRedactor.Redact("https://epg.example.org:443/guide.xml.gz");

        Assert.StartsWith("https://epg.example.org", redacted, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Redact_HandlesMissingValue(string? url)
    {
        Assert.Equal("[REDACTED]", LiveTvUrlRedactor.Redact(url));
    }

    [Fact]
    public void Redact_RedactsUnparsableValueInsteadOfEchoingIt()
    {
        // Um valor que não é URL pode ser um caminho local ou lixo com
        // credencial; nunca deve ser ecoado no log.
        var redacted = LiveTvUrlRedactor.Redact("not a url with s3cr3t inside");

        Assert.Equal("[REDACTED]", redacted);
        Assert.DoesNotContain("s3cr3t", redacted, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RedactIfRemote_KeepsLocalPathsIntactForDiagnostics()
    {
        Assert.Equal("/var/lib/mulletaflix/guide.xml", LiveTvUrlRedactor.RedactIfRemote("/var/lib/mulletaflix/guide.xml"));
        Assert.Equal(@"C:\ProgramData\guide.xml", LiveTvUrlRedactor.RedactIfRemote(@"C:\ProgramData\guide.xml"));
    }

    [Fact]
    public void RedactIfRemote_RedactsRemoteUrlCredentials()
    {
        var redacted = LiveTvUrlRedactor.RedactIfRemote("https://epg.example.org/guide.xml?api_key=abcd1234");

        Assert.DoesNotContain("abcd1234", redacted, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("epg.example.org", redacted, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_DoesNotAddPathMarkerForRootPath()
    {
        var redacted = LiveTvUrlRedactor.Redact("http://provider.example/");

        Assert.Equal("http://provider.example/", redacted);
    }
}
