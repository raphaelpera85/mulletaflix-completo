using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

[Trait("Category", "RequiresMongo")]
public sealed class NebulaDirectoryRefreshMongoTests : IDisposable
{
    private readonly string _databaseName = "nebula_directory_refresh_" + Guid.NewGuid().ToString("N");
    private readonly string _connectionString;
    private readonly string _skipReason;
    private readonly bool _available;
    private readonly MongoClient? _client;

    public NebulaDirectoryRefreshMongoTests()
    {
        var configuredConnectionString = Environment.GetEnvironmentVariable("MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING");
        if (!NebulaMongoTestConnection.TryGet(out var connectionString, out var skipReason))
        {
            if (!string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                throw new InvalidOperationException("A configuração Mongo opt-in foi definida, mas não aponta para o destino local descartável permitido.");
            }

            _connectionString = string.Empty;
            _skipReason = skipReason;
            return;
        }

        _connectionString = connectionString;
        try
        {
            var settings = MongoClientSettings.FromConnectionString(_connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
            _client = new MongoClient(settings);
            _client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            _available = true;
            _skipReason = string.Empty;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                throw new InvalidOperationException("A configuração Mongo opt-in foi definida, mas o Mongo descartável não respondeu ao ping.", ex);
            }

            _skipReason = "MongoDB de teste indisponível no destino local opt-in.";
        }
    }

    [Fact]
    public async Task PendingDirectoryRefresh_SurvivesContextRecreationAndIsRemovedAfterSuccess()
    {
        Assert.SkipUnless(_available, _skipReason);
        var cancellationToken = TestContext.Current.CancellationToken;

        using (var first = CreateContext())
        {
            await first.EnsureSchemaAsync(cancellationToken);
            await first.PersistDirectoryRefreshAsync("Series/Example/Season 1", cancellationToken);
            await first.PersistDirectoryRefreshAsync("/Series/Example/Season 1", cancellationToken);
        }

        using var restarted = CreateContext();
        var pending = await restarted.GetPendingDirectoryRefreshesAsync(cancellationToken);
        Assert.Equal(new[] { "/Series/Example/Season 1" }, pending);

        await restarted.CompleteDirectoryRefreshAsync(pending[0], cancellationToken);

        Assert.Empty(await restarted.GetPendingDirectoryRefreshesAsync(cancellationToken));
    }

    [Fact]
    public async Task CompletedUploadIsTimestampedAndReturnedByTheRefreshWindowWithOnlyRequiredFields()
    {
        Assert.SkipUnless(_available, _skipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        var database = _client!.GetDatabase(_databaseName);
        var files = database.GetCollection<BsonDocument>("files");
        var seriesId = ObjectId.GenerateNewId();
        var seasonId = ObjectId.GenerateNewId();
        var mediaId = ObjectId.GenerateNewId();
        var legacyMediaId = ObjectId.GenerateNewId();
        var now = DateTimeOffset.UtcNow;

        await files.InsertManyAsync(
            [
                new BsonDocument { ["_id"] = seriesId, ["name"] = "Example", ["type"] = "dir", ["parent"] = BsonNull.Value, ["large_payload"] = new BsonBinaryData(new byte[1024]) },
                new BsonDocument { ["_id"] = seasonId, ["name"] = "Season 1", ["type"] = "dir", ["parent"] = seriesId, ["large_payload"] = new BsonBinaryData(new byte[1024]) },
                new BsonDocument { ["_id"] = mediaId, ["name"] = "Episode.mkv", ["type"] = "file", ["status"] = "queued", ["size"] = 1024L, ["large_payload"] = new BsonBinaryData(new byte[1024]) },
                new BsonDocument
                {
                    ["_id"] = legacyMediaId,
                    ["name"] = "Legacy Episode.mkv",
                    ["type"] = "file",
                    ["status"] = "completed",
                    ["size"] = 1024L,
                    ["uploaded_at"] = now.ToUnixTimeSeconds(),
                    ["parent"] = seasonId,
                    ["parts"] = new BsonArray { new BsonDocument { ["part_number"] = 0, ["size"] = 1024L, ["tg_file_id"] = "legacy-telegram-file-id" } },
                    ["large_payload"] = new BsonBinaryData(new byte[1024])
                }
            ],
            cancellationToken: cancellationToken);

        using var context = CreateContext();
        Assert.True(await context.CompleteFileUploadAsync(
            mediaId,
            new BsonDocument
            {
                ["name"] = "Episode.mkv",
                ["type"] = "file",
                ["status"] = "completed",
                ["size"] = 1024L,
                ["parent"] = seasonId,
                ["parts"] = new BsonArray
                {
                    new BsonDocument { ["part_number"] = 0, ["size"] = 1024L, ["tg_file_id"] = "telegram-file-id" }
                }
            },
            cancellationToken));

        var recentlyCompleted = await context.GetRecentlyCompletedFilesAsync(now.AddMinutes(-1), cancellationToken);
        Assert.Equal(2, recentlyCompleted.Count);
        var completed = Assert.Single(recentlyCompleted, file => file["name"] == "Episode.mkv");
        var legacyCompleted = Assert.Single(recentlyCompleted, file => file["name"] == "Legacy Episode.mkv");

        Assert.True(completed["completed_at"].ToInt64() >= now.ToUnixTimeSeconds());
        Assert.True(NebulaMongoContext.IsCompletedTelegramMedia(completed));
        Assert.False(completed.Contains("large_payload"));
        Assert.False(legacyCompleted.Contains("completed_at"));
        Assert.True(NebulaMongoContext.IsCompletedTelegramMedia(legacyCompleted));
        Assert.False(legacyCompleted.Contains("large_payload"));
        Assert.Equal("Season 1/Example", (await context.BuildDirectoryPathMapAsync(cancellationToken))[seasonId.ToString()]);
    }

    public void Dispose()
    {
        _client?.DropDatabase(_databaseName);
    }

    private NebulaMongoContext CreateContext()
        => new(_connectionString, _databaseName, NullLogger<NebulaMongoContext>.Instance);
}
