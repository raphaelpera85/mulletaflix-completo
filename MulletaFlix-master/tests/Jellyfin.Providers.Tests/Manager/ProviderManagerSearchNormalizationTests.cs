using MediaBrowser.Providers.Manager;
using Xunit;

namespace MulletaFlix.Providers.Tests.Manager;

public sealed class ProviderManagerSearchNormalizationTests
{
    [Theory]
    [InlineData("The.Matrix.1999.1080p.WEB-DL", null, "The Matrix", 1999)]
    [InlineData("A_Matrix_1999_1080p", null, "A Matrix", 1999)]
    [InlineData("The.Matrix.1999", 2001, "The Matrix", 2001)]
    [InlineData("Model2024", null, "Model2024", null)]
    public void NormalizeRemoteSearchName_ExtractsYearFromCommonFilenameSeparators(
        string name,
        int? existingYear,
        string expectedName,
        int? expectedYear)
    {
        var normalized = ProviderManager.NormalizeRemoteSearchName(name, existingYear, out var parsedYear);

        Assert.Equal(expectedName, normalized);
        Assert.Equal(expectedYear, parsedYear);
    }

    [Fact]
    public void NormalizeRemoteSearchName_DoesNotTreatYearOnlyTitleAsReleaseYear()
    {
        var normalized = ProviderManager.NormalizeRemoteSearchName("1984", null, out var parsedYear);

        Assert.Equal("1984", normalized);
        Assert.Null(parsedYear);
    }
}
