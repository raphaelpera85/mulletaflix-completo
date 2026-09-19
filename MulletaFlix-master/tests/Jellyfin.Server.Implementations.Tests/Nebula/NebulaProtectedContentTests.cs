using System.Text.RegularExpressions;
using Jellyfin.Server.Implementations.Nebula;
using MongoDB.Bson;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Regra única do Nebula: capas, imagens, NFO/XML e legendas formam o cache local
/// de exibição e nunca podem ser excluídos do disco nem do catálogo.
/// </summary>
public class NebulaProtectedContentTests
{
    [Theory]
    [InlineData("poster.jpg", true)]
    [InlineData("fanart1.jpg", true)]
    [InlineData("A Casa do Dragão - S01E01-thumb.jpg", true)]
    [InlineData("logo.png", true)]
    [InlineData("movie.nfo", true)]
    [InlineData("Filme (2022).srt", true)]
    [InlineData("Filme (2022).strm", false)]
    [InlineData("Filme (2022).mkv", false)]
    [InlineData("Serie - S01E01.mp4", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsProtectedPath_ClassifiesCatalogCacheContent(string? path, bool expected)
    {
        Assert.Equal(expected, NebulaProtectedContent.IsProtectedPath(path));
    }

    [Fact]
    public void IsProtectedDoc_DetectsProtectedNameAndLocalPath()
    {
        var byName = new BsonDocument { { "name", "fanart1.jpg" }, { "status", "error" } };
        var byLocalPath = new BsonDocument { { "name", "asset" }, { "local_path", @"E:\NebulaStage\poster.jpg" } };
        var media = new BsonDocument { { "name", "Filme (2022).mkv" }, { "local_path", @"E:\NebulaStage\Filme (2022).mkv" } };

        Assert.True(NebulaProtectedContent.IsProtectedDoc(byName));
        Assert.True(NebulaProtectedContent.IsProtectedDoc(byLocalPath));
        Assert.False(NebulaProtectedContent.IsProtectedDoc(media));
        Assert.False(NebulaProtectedContent.IsProtectedDoc(null));
    }

    [Theory]
    [InlineData("fanart1.jpg", true)]
    [InlineData("movie.nfo", true)]
    [InlineData("legenda.srt", true)]
    [InlineData("Filme (2022).mkv", false)]
    [InlineData("serie.strm", false)]
    public void NameExpression_MatchesOnlyProtectedExtensions(string fileName, bool expected)
    {
        var pattern = NebulaProtectedContent.NameExpression.Pattern;

        Assert.Equal(expected, Regex.IsMatch(fileName, pattern, RegexOptions.IgnoreCase));
    }
}
