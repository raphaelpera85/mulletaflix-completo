using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Providers.Plugins.Tmdb;
using MediaBrowser.Providers.Plugins.Tmdb.BoxSets;
using MediaBrowser.Providers.Plugins.Tmdb.Movies;
using MediaBrowser.Providers.Plugins.Tmdb.TV;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using TMDbLib.Objects.Collections;
using TMDbLib.Objects.General;
using TMDbLib.Objects.TvShows;
using Xunit;
using LibraryBoxSet = MediaBrowser.Controller.Entities.Movies.BoxSet;
using LibraryMovie = MediaBrowser.Controller.Entities.Movies.Movie;
using TmdbMovie = TMDbLib.Objects.Movies.Movie;

namespace MulletaFlix.Providers.Tests.Tmdb;

public sealed class TmdbImageProviderLanguageTests : IDisposable
{
    private static readonly PropertyInfo InstanceProperty = typeof(Plugin).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!;
    private static readonly MethodInfo InstanceSetter = InstanceProperty.GetSetMethod(nonPublic: true)!;
    private readonly Plugin? _previousPlugin = Plugin.Instance;
    private readonly ILibraryManager? _previousLibraryManager = BaseItem.LibraryManager;

    public TmdbImageProviderLanguageTests()
    {
        var appPaths = new Mock<IApplicationPaths>();
        appPaths.Setup(paths => paths.PluginsPath).Returns(System.IO.Path.GetTempPath());
        appPaths.Setup(paths => paths.PluginConfigurationsPath).Returns(System.IO.Path.GetTempPath());

        var serializer = new Mock<IXmlSerializer>();
        serializer.Setup(value => value.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
            .Returns(new PluginConfiguration());

        _ = new Plugin(appPaths.Object, serializer.Object);
        BaseItem.LibraryManager = new Mock<ILibraryManager>().Object;
    }

    public void Dispose()
    {
        InstanceSetter.Invoke(null, new object?[] { _previousPlugin });
        BaseItem.LibraryManager = _previousLibraryManager;
    }

    [Fact]
    public async Task ImageProviders_PreserveRegionWhenRequestingAllLanguages()
    {
        const string language = "pt-BR";
        const string country = "BR";
        const string imageRegion = "PT";
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var manager = new TmdbClientManager(cache);

        var movie = new LibraryMovie();
        movie.SetProviderId(MetadataProvider.Tmdb, "42");
        cache.Set(CacheKey("movie", 42, language, country), new TmdbMovie { Images = Images() });

        var series = new Series();
        series.SetProviderId(MetadataProvider.Tmdb, "43");
        series.DisplayOrder = "aired";
        var seriesId = Guid.NewGuid();
        var libraryManager = Mock.Get(BaseItem.LibraryManager);
        libraryManager.Setup(value => value.GetItemById(seriesId)).Returns(series);
        cache.Set(CacheKey("series", 43, language, country), new TvShow { Images = Images() });
        cache.Set(CacheKey("season-s1", 43, language, country), new TvSeason { Images = new PosterImages { Posters = [Image(imageRegion)] } });
        cache.Set(CacheKey("episode-s1e1-aired", 43, language, country), new TvEpisode { Images = new StillImages { Stills = [Image(imageRegion)] } });

        var season = new Season { SeriesId = seriesId, IndexNumber = 1 };
        var episode = new Episode { SeriesId = seriesId, ParentIndexNumber = 1, IndexNumber = 1 };

        var boxSet = new LibraryBoxSet();
        boxSet.SetProviderId(MetadataProvider.Tmdb, "44");
        cache.Set(CacheKey("collection", 44, language, country), new Collection { Images = Images() });

        var providers = new (BaseItem Item, Func<BaseItem, CancellationToken, Task<IEnumerable<MediaBrowser.Model.Providers.RemoteImageInfo>>> GetImages)[]
        {
            (movie, (item, token) => new TmdbMovieImageProvider(null!, manager).GetImages(item, language, country, true, token)),
            (series, (item, token) => new TmdbSeriesImageProvider(null!, manager).GetImages(item, language, country, true, token)),
            (season, (item, token) => new TmdbSeasonImageProvider(null!, manager).GetImages(item, language, country, true, token)),
            (episode, (item, token) => new TmdbEpisodeImageProvider(null!, manager).GetImages(item, language, country, true, token)),
            (boxSet, (item, token) => new TmdbBoxSetImageProvider(null!, manager).GetImages(item, language, country, true, token))
        };

        foreach (var (item, getImages) in providers)
        {
            var images = await getImages(item, CancellationToken.None);
            Assert.NotEmpty(images);
            Assert.All(images, image => Assert.Equal("pt-PT", image.Language));
        }
    }

    private static string CacheKey(string kind, int id, string language, string country)
    {
        var queryLanguage = TmdbUtils.GetImageQueryLanguage(language, includeAllLanguages: true);
        var imageLanguages = TmdbUtils.GetImageLanguagesParam(queryLanguage, country, includeAllLanguages: true);
        return TmdbClientManager.CreateImageAwareCacheKey(kind, id, queryLanguage, imageLanguages, country);
    }

    private static Images Images() => new()
    {
        Posters = [Image("PT")],
        Backdrops = [Image("PT")],
        Logos = [Image("PT")]
    };

    private static ImageData Image(string region) => new()
    {
        Iso_639_1 = "pt",
        Iso_3166_1 = region
    };
}
