using System;
using System.IO;
using System.Text;
using MediaBrowser.Providers.Plugins.Tmdb;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Search;
using TMDbLib.Utilities.Serializer;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.Tmdb;

public sealed class TmdbJsonSerializerDateTimeTests
{
    private static MemoryStream CreateStream(string json)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    [Fact]
    public void StandardSerializer_FailsOnUtcFormattedPublishDate()
    {
        const string json = "{\"id\":\"123\",\"key\":\"xyz\",\"published_at\":\"2013-01-18 05:00:09 UTC\"}";
        using var stream = CreateStream(json);

        // Standard serializer reproduces upstream Jellyfin issue #16722 / #13176
        var standard = TMDbJsonSerializer.Instance;
        Assert.ThrowsAny<Exception>(() => standard.Deserialize(stream, typeof(Video)));
    }

    [Fact]
    public void TolerantSerializer_ParsesUtcFormattedPublishDate()
    {
        const string json = "{\"id\":\"123\",\"key\":\"xyz\",\"published_at\":\"2013-01-18 05:00:09 UTC\"}";
        using var stream = CreateStream(json);

        var serializer = TolerantTmdbJsonSerializer.Instance;
        var result = serializer.Deserialize(stream, typeof(Video)) as Video;

        Assert.NotNull(result);
        Assert.Equal("123", result.Id);
        Assert.Equal("xyz", result.Key);
        Assert.Equal(new DateTime(2013, 1, 18, 5, 0, 9, DateTimeKind.Utc), result.PublishedAt.ToUniversalTime());
    }

    [Fact]
    public void TolerantSerializer_ParsesIso8601PublishDate()
    {
        const string json = "{\"id\":\"456\",\"key\":\"abc\",\"published_at\":\"2021-04-12T15:30:45.000Z\"}";
        using var stream = CreateStream(json);

        var serializer = TolerantTmdbJsonSerializer.Instance;
        var result = serializer.Deserialize(stream, typeof(Video)) as Video;

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2021, 4, 12, 15, 30, 45, DateTimeKind.Utc), result.PublishedAt.ToUniversalTime());
    }

    [Fact]
    public void TolerantSerializer_HandlesEmptyOrWhitespacePublishDateWithoutThrowing()
    {
        const string json = "{\"id\":\"789\",\"key\":\"def\",\"published_at\":\"\"}";
        using var stream = CreateStream(json);

        var serializer = TolerantTmdbJsonSerializer.Instance;
        var result = serializer.Deserialize(stream, typeof(Video)) as Video;

        Assert.NotNull(result);
        Assert.Equal(default(DateTime), result.PublishedAt);
    }

    [Fact]
    public void TolerantSerializer_ParsesValidReleaseDateOnSearchMovie()
    {
        const string json = "{\"id\":550,\"title\":\"Fight Club\",\"release_date\":\"1999-10-15\"}";
        using var stream = CreateStream(json);

        var serializer = TolerantTmdbJsonSerializer.Instance;
        var result = serializer.Deserialize(stream, typeof(SearchMovie)) as SearchMovie;

        Assert.NotNull(result);
        Assert.Equal(550, result.Id);
        Assert.True(result.ReleaseDate.HasValue);
        Assert.Equal(1999, result.ReleaseDate.Value.Year);
        Assert.Equal(10, result.ReleaseDate.Value.Month);
        Assert.Equal(15, result.ReleaseDate.Value.Day);
    }

    [Fact]
    public void TolerantSerializer_HandlesInvalidReleaseDateOnSearchMovieWithoutThrowing()
    {
        const string json = "{\"id\":551,\"title\":\"Unknown\",\"release_date\":\"not-a-valid-date\"}";
        using var stream = CreateStream(json);

        var serializer = TolerantTmdbJsonSerializer.Instance;
        var result = serializer.Deserialize(stream, typeof(SearchMovie)) as SearchMovie;

        Assert.NotNull(result);
        Assert.Equal(551, result.Id);
        Assert.Null(result.ReleaseDate);
    }
}
