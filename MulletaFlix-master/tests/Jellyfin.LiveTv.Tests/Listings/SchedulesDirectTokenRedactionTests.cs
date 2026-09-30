using MulletaFlix.LiveTv.Listings;
using Xunit;

namespace Jellyfin.LiveTv.Tests.Listings;

public class SchedulesDirectTokenRedactionTests
{
    [Fact]
    public void RedactToken_NeverRevealsTheSecretValue()
    {
        const string Token = "f3a91c7d5e2b48a6901fbc3d7e5a2b19";

        var redacted = SchedulesDirect.RedactToken(Token);

        // O valor completo nunca pode aparecer no log.
        Assert.DoesNotContain(Token, redacted, System.StringComparison.Ordinal);

        // Um sufixo curto ajuda a correlacionar sessões sem revelar o segredo:
        // no máximo 4 caracteres do token original.
        Assert.Contains("…", redacted, System.StringComparison.Ordinal);
        Assert.Contains("2b19", redacted, System.StringComparison.Ordinal);
        Assert.DoesNotContain("f3a91c7d", redacted, System.StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RedactToken_HandlesMissingTokenWithoutLeakingOrThrowing(string? token)
    {
        var redacted = SchedulesDirect.RedactToken(token);

        Assert.Equal("[REDACTED]", redacted);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abcd")]
    [InlineData("abcdefgh")]
    public void RedactToken_DoesNotExposeShortTokens(string token)
    {
        var redacted = SchedulesDirect.RedactToken(token);

        // Tokens curtos não têm entropia suficiente para expor qualquer parte.
        Assert.Equal("[REDACTED]", redacted);
        Assert.DoesNotContain(token, redacted, System.StringComparison.Ordinal);
    }
}
