using MediaBrowser.Providers.Plugins.ShortMax;
using Xunit;

namespace Jellyfin.Providers.Tests.Plugins.ShortMax;

public sealed class ShortMaxTitleMatcherTests
{
    [Theory]
    [InlineData("https://shorttv.live/pt/drama/dubladoamar-cair-e-recomecar-21293", "21293")]
    [InlineData("/pt/episode/dubladoamar-cair-e-recomecar-21293-24", "21293")]
    [InlineData("21293", "21293")]
    public void ExtractSeriesIdReturnsDramaId(string input, string expected)
    {
        Assert.Equal(expected, ShortMaxTitleMatcher.ExtractSeriesId(input));
    }

    [Fact]
    public void SimilarityIgnoresAccentsAndDubladoPrefix()
    {
        Assert.Equal(1, ShortMaxTitleMatcher.Similarity("[Dublado]Amar, Cair e Recomeçar", "Amar Cair e Recomecar"));
    }
}
