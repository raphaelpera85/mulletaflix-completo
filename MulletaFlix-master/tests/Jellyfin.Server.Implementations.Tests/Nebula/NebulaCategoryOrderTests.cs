using Jellyfin.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// A ordem de categorias do Nebula precisa ter uma fonte única.
/// </summary>
/// <remarks>
/// A sequência estava duplicada em três lugares: o rank numérico em
/// `GetCategoryPriority`, o nome exibido em `GetCategoryDisplayName` e os nomes
/// de pasta em `NebulaStrmGenerator`. Duas tabelas paralelas mantidas à mão
/// divergem: mudar a ordem num lugar e esquecer o outro faz o painel anunciar
/// uma sequência diferente da que o downloader executa, que é exatamente o
/// desalinhamento que T2.3 exige eliminar.
/// </remarks>
public class NebulaCategoryOrderTests
{
    [Theory]
    [InlineData("ANIMACAO", 1)]
    [InlineData("FILME", 2)]
    [InlineData("SERIE", 3)]
    [InlineData("DORAMA", 4)]
    [InlineData("NOVELA", 5)]
    [InlineData("PORNO", 6)]
    public void GetRank_FollowsTheConfiguredDownloadOrder(string mediaType, int expectedRank)
    {
        Assert.Equal(expectedRank, NebulaCategoryOrder.GetRank(mediaType));
    }

    [Fact]
    public void GetRank_PutsUnknownCategoriesLast()
    {
        var unknown = NebulaCategoryOrder.GetRank("QUALQUER_OUTRA");

        Assert.True(unknown > NebulaCategoryOrder.GetRank("PORNO"));
        Assert.Equal(unknown, NebulaCategoryOrder.GetRank(null));
        Assert.Equal(unknown, NebulaCategoryOrder.GetRank(string.Empty));
    }

    [Fact]
    public void GetRank_IsCaseInsensitive()
    {
        Assert.Equal(
            NebulaCategoryOrder.GetRank("ANIMACAO"),
            NebulaCategoryOrder.GetRank("animacao"));
    }

    [Theory]
    [InlineData(1, "ANIMAÇÕES")]
    [InlineData(2, "FILMES")]
    [InlineData(3, "SERIES")]
    [InlineData(4, "DORAMAS")]
    [InlineData(5, "NOVELAS")]
    [InlineData(6, "PORNO")]
    public void GetDisplayName_MatchesTheRank(int rank, string expected)
    {
        Assert.Equal(expected, NebulaCategoryOrder.GetDisplayName(rank));
    }

    [Fact]
    public void GetDisplayName_FallsBackForUnknownRank()
    {
        Assert.Equal("OUTROS", NebulaCategoryOrder.GetDisplayName(99));
        Assert.Equal("OUTROS", NebulaCategoryOrder.GetDisplayName(0));
    }

    [Fact]
    public void RankAndDisplayName_StayConsistentAcrossTheWholeOrder()
    {
        // O invariante que a duplicação quebrava: todo rank derivado de um tipo
        // conhecido precisa resolver para um nome exibido próprio, nunca para o
        // rótulo de fallback.
        foreach (var mediaType in NebulaCategoryOrder.OrderedMediaTypes)
        {
            var rank = NebulaCategoryOrder.GetRank(mediaType);
            var display = NebulaCategoryOrder.GetDisplayName(rank);

            Assert.NotEqual("OUTROS", display);
        }
    }

    [Fact]
    public void OrderedMediaTypes_ExposesTheSequenceWithoutGapsOrRepeats()
    {
        var ranks = new System.Collections.Generic.List<int>();
        foreach (var mediaType in NebulaCategoryOrder.OrderedMediaTypes)
        {
            ranks.Add(NebulaCategoryOrder.GetRank(mediaType));
        }

        // A interface usa esta lista para anunciar a ordem: ela precisa ser
        // estritamente crescente a partir de 1, senão o painel exibe uma
        // sequência que o downloader não segue.
        for (var index = 0; index < ranks.Count; index++)
        {
            Assert.Equal(index + 1, ranks[index]);
        }
    }

    [Fact]
    public void DownloaderUsesTheSharedOrder()
    {
        // Prova que o downloader não mantém mais a própria tabela.
        var animationPath = System.IO.Path.Combine("N:", "Animações", "Serie", "ep1.strm");
        var moviePath = System.IO.Path.Combine("N:", "Filmes", "Filme (2020)", "filme.strm");

        Assert.Equal(
            NebulaDownloaderEngine.GetCategoryPriority(animationPath),
            NebulaCategoryOrder.GetRank(
                NebulaUploadEngine.ClassifyMediaType(
                    System.IO.Path.GetDirectoryName(animationPath),
                    System.IO.Path.GetFileName(animationPath))));

        Assert.Equal(
            NebulaDownloaderEngine.GetCategoryPriority(moviePath),
            NebulaCategoryOrder.GetRank(
                NebulaUploadEngine.ClassifyMediaType(
                    System.IO.Path.GetDirectoryName(moviePath),
                    System.IO.Path.GetFileName(moviePath))));
    }
}
