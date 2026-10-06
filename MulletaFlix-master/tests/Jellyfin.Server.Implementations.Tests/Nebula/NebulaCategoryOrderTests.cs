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
[Collection("NebulaCategoryOrder")]
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

    [Fact]
    public void ApplyCustomOrder_ReordersRanksAndDisplayNames()
    {
        try
        {
            NebulaCategoryOrder.ApplyCustomOrder(new[] { "PORNO", "ANIMACAO", "FILME", "SERIE", "DORAMA", "NOVELA" });

            Assert.Equal(1, NebulaCategoryOrder.GetRank("PORNO"));
            Assert.Equal(2, NebulaCategoryOrder.GetRank("ANIMACAO"));
            Assert.Equal("PORNO", NebulaCategoryOrder.GetDisplayName(1));
            Assert.Equal("ANIMAÇÕES", NebulaCategoryOrder.GetDisplayName(2));
            Assert.Equal(
                new[] { "PORNO", "ANIMACAO", "FILME", "SERIE", "DORAMA", "NOVELA" },
                NebulaCategoryOrder.OrderedMediaTypes);
        }
        finally
        {
            NebulaCategoryOrder.ApplyCustomOrder(null);
        }
    }

    [Fact]
    public void ApplyCustomOrder_IgnoresUnknownAndDuplicateEntries()
    {
        try
        {
            NebulaCategoryOrder.ApplyCustomOrder(new[] { "FILME", "QUALQUER_OUTRA", "FILME", "SERIE" });

            // FILME fica na frente (primeira menção válida); entradas desconhecidas
            // ou repetidas são descartadas; categorias ausentes são acrescentadas ao
            // final na ordem padrão, então o resultado continua sendo uma permutação
            // completa das 6 categorias conhecidas.
            Assert.Equal(
                new[] { "FILME", "SERIE", "ANIMACAO", "DORAMA", "NOVELA", "PORNO" },
                NebulaCategoryOrder.OrderedMediaTypes);
        }
        finally
        {
            NebulaCategoryOrder.ApplyCustomOrder(null);
        }
    }

    [Fact]
    public void ApplyCustomOrder_WithNullOrEmpty_RestoresFactoryDefault()
    {
        NebulaCategoryOrder.ApplyCustomOrder(new[] { "PORNO", "ANIMACAO" });
        NebulaCategoryOrder.ApplyCustomOrder(null);

        Assert.Equal(NebulaCategoryOrder.DefaultMediaTypeOrder, NebulaCategoryOrder.OrderedMediaTypes);

        NebulaCategoryOrder.ApplyCustomOrder(new[] { "PORNO", "ANIMACAO" });
        NebulaCategoryOrder.ApplyCustomOrder(System.Array.Empty<string>());

        Assert.Equal(NebulaCategoryOrder.DefaultMediaTypeOrder, NebulaCategoryOrder.OrderedMediaTypes);
    }

    [Theory]
    [InlineData(new[] { "ANIMACAO", "FILME", "SERIE", "DORAMA", "NOVELA", "PORNO" }, true)]
    [InlineData(new[] { "FILME", "SERIE" }, true)]
    [InlineData(new string[0], true)]
    [InlineData(new[] { "FILME", "FILME" }, false)]
    [InlineData(new[] { "FILME", "NAO_EXISTE" }, false)]
    public void IsValidCustomOrder_RejectsDuplicatesAndUnknownCategories(string[] order, bool expected)
    {
        Assert.Equal(expected, NebulaCategoryOrder.IsValidCustomOrder(order));
    }

    [Fact]
    public void IsValidCustomOrder_RejectsNullEntry()
    {
        Assert.False(NebulaCategoryOrder.IsValidCustomOrder(new[] { "FILME", null! }));
    }
}
