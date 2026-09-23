using Emby.Naming.Common;
using Emby.Naming.TV;
using Xunit;

namespace MulletaFlix.Naming.Tests.TV
{
    /// <summary>
    /// Covers the year parsed out of a series folder name.
    /// </summary>
    /// <remarks>
    /// The resolver used to strip "(2020)" from the name and throw the value away. Without the year
    /// every provider receives two identical lookup requests for "A Agencia (2020)" and
    /// "A Agencia (2024)", both folders match the same TMDb entry, and the two shows end up with the
    /// same provider ids — which is what makes them share a presentation unique key and appear to the
    /// client as a single merged series.
    /// </remarks>
    public class SeriesResolverYearTests
    {
        private readonly NamingOptions _namingOptions = new NamingOptions();

        [Theory]
        [InlineData("/some/path/A Agencia (2020)", "A Agencia", 2020)]
        [InlineData("/some/path/A Agencia (2024)", "A Agencia", 2024)]
        [InlineData("/some/path/1923 (2022)", "1923", 2022)]
        [InlineData("/some/path/The Show (2019)", "The Show", 2019)]
        public void Resolve_KeepsTheYearAndStripsItFromTheName(string path, string expectedName, int expectedYear)
        {
            var resolved = SeriesResolver.Resolve(_namingOptions, path);

            Assert.Equal(expectedName, resolved.Name);
            Assert.Equal(expectedYear, resolved.Year);
        }

        [Theory]
        [InlineData("/some/path/The Show")]
        [InlineData("/some/path/The Show s02e10 720p hdtv")]
        [InlineData("/some/path/The_Show_Season_1")]
        public void Resolve_LeavesTheYearNullWhenTheNameCarriesNone(string path)
        {
            var resolved = SeriesResolver.Resolve(_namingOptions, path);

            Assert.Null(resolved.Year);
        }

        [Fact]
        public void Resolve_TellsTwoSameNamedSeriesApartByYear()
        {
            var first = SeriesResolver.Resolve(_namingOptions, "/some/path/A Agencia (2020)");
            var second = SeriesResolver.Resolve(_namingOptions, "/some/path/A Agencia (2024)");

            // Same display name, different lookup info: this is what the providers need in order to
            // stop matching both folders to the same series.
            Assert.Equal(first.Name, second.Name);
            Assert.NotEqual(first.Year, second.Year);
            Assert.Equal(2020, first.Year);
            Assert.Equal(2024, second.Year);
        }
    }
}
