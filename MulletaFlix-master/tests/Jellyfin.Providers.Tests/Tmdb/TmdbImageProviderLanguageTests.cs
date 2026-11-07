using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
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
using TMDbLib.Client;
using TMDbLib.Objects.Collections;
using TMDbLib.Objects.General;
using TMDbLib.Objects.TvShows;
using Xunit;
using LibraryBoxSet = MediaBrowser.Controller.Entities.Movies.BoxSet;
using LibraryMovie = MediaBrowser.Controller.Entities.Movies.Movie;
using TmdbMovie = TMDbLib.Objects.Movies.Movie;

namespace MulletaFlix.Providers.Tests.Tmdb;

[Collection("LibraryManagerTests")]
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImageProviders_PreserveRegionalLabelsAndEnglishFallback(bool includeAllLanguages)
    {
        const string language = "pt-BR";
        const string country = "BR";
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var manager = new TmdbClientManager(cache);

        var movie = new LibraryMovie();
        movie.SetProviderId(MetadataProvider.Tmdb, "42");
        cache.Set(CacheKey("movie", 42, language, country, includeAllLanguages), new TmdbMovie { Images = Images() });

        var series = new Series();
        series.SetProviderId(MetadataProvider.Tmdb, "43");
        series.DisplayOrder = "aired";
        var seriesId = Guid.NewGuid();
        var libraryManager = Mock.Get(BaseItem.LibraryManager);
        libraryManager.Setup(value => value.GetItemById(seriesId)).Returns(series);
        cache.Set(CacheKey("series", 43, language, country, includeAllLanguages), new TvShow { Images = Images() });
        cache.Set(CacheKey("season-s1", 43, language, country, includeAllLanguages), new TvSeason { Images = new PosterImages { Posters = Images().Posters } });
        cache.Set(CacheKey("episode-s1e1-aired", 43, language, country, includeAllLanguages), new TvEpisode { Images = new StillImages { Stills = Images().Posters } });

        var season = new Season { SeriesId = seriesId, IndexNumber = 1 };
        var episode = new Episode { SeriesId = seriesId, ParentIndexNumber = 1, IndexNumber = 1 };

        var boxSet = new LibraryBoxSet();
        boxSet.SetProviderId(MetadataProvider.Tmdb, "44");
        cache.Set(CacheKey("collection", 44, language, country, includeAllLanguages), new Collection { Images = Images() });

        var providers = new (BaseItem Item, Func<BaseItem, CancellationToken, Task<IEnumerable<MediaBrowser.Model.Providers.RemoteImageInfo>>> GetImages)[]
        {
            (movie, (item, token) => new TmdbMovieImageProvider(null!, manager).GetImages(item, language, country, includeAllLanguages, token)),
            (series, (item, token) => new TmdbSeriesImageProvider(null!, manager).GetImages(item, language, country, includeAllLanguages, token)),
            (season, (item, token) => new TmdbSeasonImageProvider(null!, manager).GetImages(item, language, country, includeAllLanguages, token)),
            (episode, (item, token) => new TmdbEpisodeImageProvider(null!, manager).GetImages(item, language, country, includeAllLanguages, token)),
            (boxSet, (item, token) => new TmdbBoxSetImageProvider(null!, manager).GetImages(item, language, country, includeAllLanguages, token))
        };

        foreach (var (item, getImages) in providers)
        {
            var images = await getImages(item, CancellationToken.None);
            var imageList = images.ToArray();
            Assert.Contains(imageList, image => image.Language == "pt-BR");
            Assert.Contains(imageList, image => image.Language == "pt-PT");
            Assert.Contains(imageList, image => image.Language == "en");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ImageProviders_SendConfiguredImageLanguageFilters(bool includeAllLanguages)
    {
        const string language = "pt-BR";
        const string country = "BR";
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new RecordingHandler();
        using var manager = CreateManager(cache, handler);

        var movie = new LibraryMovie();
        movie.SetProviderId(MetadataProvider.Tmdb, "42");

        var series = new Series { DisplayOrder = "aired" };
        series.SetProviderId(MetadataProvider.Tmdb, "43");
        var seriesId = Guid.NewGuid();
        Mock.Get(BaseItem.LibraryManager).Setup(value => value.GetItemById(seriesId)).Returns(series);

        var season = new Season { SeriesId = seriesId, IndexNumber = 1 };
        var episode = new Episode { SeriesId = seriesId, ParentIndexNumber = 1, IndexNumber = 1 };

        var boxSet = new LibraryBoxSet();
        boxSet.SetProviderId(MetadataProvider.Tmdb, "44");

        var providers = new (BaseItem Item, Func<BaseItem, CancellationToken, Task<IEnumerable<MediaBrowser.Model.Providers.RemoteImageInfo>>> GetImages)[]
        {
            (movie, (item, token) => new TmdbMovieImageProvider(null!, manager).GetImages(item, language, country, includeAllLanguages, token)),
            (series, (item, token) => new TmdbSeriesImageProvider(null!, manager).GetImages(item, language, country, includeAllLanguages, token)),
            (season, (item, token) => new TmdbSeasonImageProvider(null!, manager).GetImages(item, language, country, includeAllLanguages, token)),
            (episode, (item, token) => new TmdbEpisodeImageProvider(null!, manager).GetImages(item, language, country, includeAllLanguages, token)),
            (boxSet, (item, token) => new TmdbBoxSetImageProvider(null!, manager).GetImages(item, language, country, includeAllLanguages, token))
        };

        foreach (var (item, getImages) in providers)
        {
            _ = await getImages(item, CancellationToken.None);
        }

        var imageRequests = handler.Requests.Where(uri => !uri.AbsolutePath.EndsWith("/configuration", StringComparison.Ordinal)).ToArray();
        Assert.Equal(5, imageRequests.Length);
        Assert.All(imageRequests, uri =>
        {
            var query = Uri.UnescapeDataString(uri.Query.TrimStart('?')).Split('&');
            var languageFilter = query.SingleOrDefault(parameter => parameter.StartsWith("language=", StringComparison.Ordinal));
            var imageLanguageFilter = query.SingleOrDefault(parameter => parameter.StartsWith("include_image_language=", StringComparison.Ordinal));
            if (includeAllLanguages)
            {
                Assert.True(string.IsNullOrEmpty(languageFilter) || languageFilter == "language=");
                Assert.True(string.IsNullOrEmpty(imageLanguageFilter) || imageLanguageFilter == "include_image_language=");
            }
            else
            {
                Assert.Equal("language=pt-BR", languageFilter);
                Assert.Equal("include_image_language=pt-BR,pt,null,en", imageLanguageFilter);
            }
        });
    }

    private static TmdbClientManager CreateManager(IMemoryCache cache, RecordingHandler handler)
    {
        var manager = new TmdbClientManager(cache);
        var clientField = typeof(TmdbClientManager).GetField("_tmDbClient", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((TMDbClient)clientField.GetValue(manager)!).Dispose();
        var clientConstructor = typeof(TMDbClient)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 6);
        clientField.SetValue(manager, (TMDbClient)clientConstructor.Invoke(["test-api-key", true, "api.themoviedb.org", null, null, handler]));
        return manager;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }

    private static string CacheKey(string kind, int id, string language, string country, bool includeAllLanguages = true)
    {
        var queryLanguage = TmdbUtils.GetImageQueryLanguage(language, includeAllLanguages);
        var imageLanguages = TmdbUtils.GetImageLanguagesParam(queryLanguage, country, includeAllLanguages);
        return TmdbClientManager.CreateImageAwareCacheKey(kind, id, queryLanguage, imageLanguages, country);
    }

    private static Images Images() => new()
    {
        Posters = [Image("pt", "BR"), Image("pt", "PT"), Image("en", "US")],
        Backdrops = [Image("pt", "BR"), Image("pt", "PT"), Image("en", "US")],
        Logos = [Image("pt", "BR"), Image("pt", "PT"), Image("en", "US")]
    };

    private static ImageData Image(string language, string region) => new()
    {
        Iso_639_1 = language,
        Iso_3166_1 = region
    };
}
