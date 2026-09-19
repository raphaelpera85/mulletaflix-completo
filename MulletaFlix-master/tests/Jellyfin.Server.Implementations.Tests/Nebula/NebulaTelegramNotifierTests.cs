using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaTelegramNotifierTests
{
    [Fact]
    public void HasTelegramParts_ReturnsFalse_WhenCompletedMetadataHasNoUploadedPart()
    {
        var document = new BsonDocument
        {
            { "name", "Rebelde (2022) - S01E01.mkv" },
            { "status", "completed" },
            { "metadata", new BsonDocument { { "title", "Rebelde" } } }
        };

        Assert.False(NebulaMongoContext.HasTelegramParts(document));
    }

    [Fact]
    public void HasTelegramParts_ReturnsTrue_WhenUploadedPartHasTelegramFileId()
    {
        var document = new BsonDocument
        {
            { "status", "completed" },
            {
                "parts",
                new BsonArray
                {
                    new BsonDocument
                    {
                        { "tg_file_id", "telegram-file-id" }
                    }
                }
            }
        };

        Assert.True(NebulaMongoContext.HasTelegramParts(document));
    }

    [Fact]
    public void EpisodeIdentity_PreventsMovieIdentityFromCollapsingEpisodes()
    {
        var episode = NebulaDownloaderEngine.EpisodeIdentity(
            "Rebelde (2022)",
            "Rebelde (2022) - S01E02.strm");

        Assert.True(episode.HasValue);
        Assert.Equal("rebelde 2022", episode.Value.Series);
        Assert.Equal(1, episode.Value.Season);
        Assert.Equal(2, episode.Value.Episode);
    }

    [Fact]
    public void BuildDirectWebUrl_UsesPublicServerAndNeverLocalhost()
    {
        var itemId = Guid.Parse("e7b83cbe-1f78-2b32-9a24-90a40252e46a");
        var url = NotificationsLibraryNotifier.BuildDirectWebUrl(itemId, "http://mulletaflix.duckdns.org:8096/", "29ebbf0c786245469ecb2cccbb257f60");

        Assert.Equal("http://mulletaflix.duckdns.org:8096/web/#/details?id=e7b83cbe1f782b329a2490a40252e46a&serverId=29ebbf0c786245469ecb2cccbb257f60", url);
        Assert.DoesNotContain("localhost", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFalse_WhenNoBotTokensConfigured()
    {
        await using var pool = new NebulaTelegramPool(
            12345,
            "test_hash",
            new List<string>(),
            -100123456789,
            string.Empty,
            NullLogger<NebulaTelegramPool>.Instance);

        var result = await pool.SendMessageAsync("<b>Teste</b>", null, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFalse_WhenMessageIsEmpty()
    {
        await using var pool = new NebulaTelegramPool(
            12345,
            "test_hash",
            new[] { "bot123:token" },
            -100123456789,
            string.Empty,
            NullLogger<NebulaTelegramPool>.Instance);

        var result = await pool.SendMessageAsync(string.Empty, null, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFalse_WhenChatIdIsZeroOrEmpty()
    {
        await using var pool = new NebulaTelegramPool(
            12345,
            "test_hash",
            new[] { "bot123:token" },
            0,
            string.Empty,
            NullLogger<NebulaTelegramPool>.Instance);

        var result = await pool.SendMessageAsync("<b>Teste</b>", string.Empty, CancellationToken.None);
        Assert.False(result);
    }
}
