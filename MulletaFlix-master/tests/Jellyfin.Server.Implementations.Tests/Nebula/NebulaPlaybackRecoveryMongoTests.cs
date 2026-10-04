using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Exercises recovery from persisted Mongo media metadata through a failed and recovered Telegram fetch.
/// </summary>
[Trait("Category", "RequiresMongo")]
public sealed class NebulaPlaybackRecoveryMongoTests : IDisposable
{
    private readonly string _databaseName = "nebula_playback_recovery_" + Guid.NewGuid().ToString("N");
    private readonly string _connectionString;
    private readonly string _skipReason;
    private readonly bool _available;
    private readonly MongoClient? _client;

    public NebulaPlaybackRecoveryMongoTests()
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
    public async Task PersistedMedia_RetriesFailedTelegramFetchAfterContextRecreationAndUsesDiskCache()
    {
        Assert.SkipUnless(_available, _skipReason);

        const int chunkSize = 1024 * 1024;
        var expectedBytes = new byte[chunkSize + 128];
        for (var index = 0; index < expectedBytes.Length; index++)
        {
            expectedBytes[index] = (byte)(index % 251);
        }

        var localPath = Path.Combine(Path.GetTempPath(), "nebula-playback-recovery", Guid.NewGuid().ToString("N"), "episode.mkv");
        var uploadedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var uploadedPart = new BsonDocument
        {
            { "part_number", 0 },
            { "part_id", 0 },
            { "size", (long)expectedBytes.Length },
            { "file_size", (long)expectedBytes.Length },
            { "bot_index", 0 },
            { "tg_file_id", "isolated-telegram-file" },
            { "tg_file", "isolated-telegram-file" },
            { "tg_message_id", 456L },
            { "tg_message", 456L },
            { "tg_chat_id", 123L },
            { "status", "uploaded" },
            { "chunk_name", "isolated-upload.part_000" },
            { "caption", "isolated playback recovery test" },
            { "uploaded_at", uploadedAt }
        };
        var finalFields = new BsonDocument
        {
            { "name", "episode.mkv" },
            { "type", "file" },
            { "is_directory", false },
            { "status", "completed" },
            { "size", (long)expectedBytes.Length },
            { "file_uuid", "isolated-upload" },
            { "media_type", "video" },
            { "parent", BsonNull.Value },
            { "parts", new BsonArray { uploadedPart } },
            { "uploaded_at", uploadedAt },
            { "modified_at", uploadedAt },
            { "tg_message_id", 456L },
            { "tg_chat_id", 123L },
            { "tg_file_id", "isolated-telegram-file" }
        };

        using (var producerContext = CreateContext())
        {
            Assert.True(await producerContext.InsertFileDocIfAbsentAsync(new BsonDocument
            {
                { "name", "episode.mkv" },
                { "type", "file" },
                { "is_directory", false },
                { "status", "queued" },
                { "size", (long)expectedBytes.Length },
                { "parts", new BsonArray() },
                { "local_path", localPath },
                { "queued_at", uploadedAt },
                { "modified_at", uploadedAt }
            }));
            var queuedMedia = Assert.Single(await producerContext.GetQueuedUploadsAsync());
            var mediaId = queuedMedia["_id"].AsObjectId;
            Assert.NotNull(await producerContext.ClaimFileForUploadAsync(mediaId, workerId: 7, botIndex: 0));
            Assert.True(await producerContext.UpdateUploadProgressAsync(
                mediaId,
                new BsonArray { uploadedPart },
                expectedBytes.Length,
                lastBotIndex: 0,
                workerId: "7"));
            Assert.True(await producerContext.CompleteFileUploadAsync(
                mediaId,
                finalFields,
                workerId: "7"));
        }

        BsonDocument persistedMedia;
        ObjectId persistedMediaId;
        using (var firstContext = CreateContext())
        {
            var foundMedia = Assert.IsType<BsonDocument>(await firstContext.FindFileByVirtualPathOrNameAsync("episode.mkv"));
            persistedMediaId = foundMedia["_id"].AsObjectId;
        }

        // Recreate the context to prove that persisted metadata remains readable beyond its original context.
        using (var restartedContext = CreateContext())
        {
            persistedMedia = Assert.IsType<BsonDocument>(
                await restartedContext.FindFileByVirtualPathOrNameAsync("episode.mkv"));
        }

        var (parts, totalSize) = NebulaHttpStreamServer.BuildStreamParts(persistedMedia);
        var part = Assert.Single(parts);
        Assert.Equal(persistedMediaId, persistedMedia["_id"].AsObjectId);
        Assert.Equal("isolated-telegram-file", part.FileId);
        Assert.Equal(123L, part.ChatId);
        Assert.Equal(456, part.MessageId);
        Assert.Equal(expectedBytes.Length, totalSize);

        var cacheRoot = Path.Combine(Path.GetTempPath(), "mulletaflix-playback-recovery-" + Guid.NewGuid().ToString("N"));
        var cache = new NebulaPlaybackCache(cacheRoot, NullLogger<NebulaPlaybackCache>.Instance);
        var cacheAccessor = new NebulaPlaybackCacheAccessor();
        cacheAccessor.Set(cache);
        var fetchAttempts = 0;
        var prefetchTasks = new List<Task>();
        var firstFetchStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstFetchFailure = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<byte[]> FetchChunk(NebulaStreamPart requestedPart, int chunkIndex, CancellationToken cancellationToken)
        {
            Assert.Equal("isolated-telegram-file", requestedPart.FileId);
            Assert.InRange(chunkIndex, 0, 1);
            fetchAttempts++;
            if (fetchAttempts == 1)
            {
                firstFetchStarted.TrySetResult(true);
                await allowFirstFetchFailure.Task.WaitAsync(cancellationToken);
                throw new HttpRequestException("Simulated Telegram outage");
            }

            var offset = chunkIndex * chunkSize;
            var count = Math.Min(chunkSize, expectedBytes.Length - offset);
            return expectedBytes.AsSpan(offset, count).ToArray();
        }

        try
        {
            await using (var failedStream = new NebulaChunkedStream(
                null,
                parts,
                totalSize,
                NullLogger.Instance,
                cacheAccessor,
                "mongo:" + persistedMediaId,
                acquirePlaybackLease: true,
                FetchChunk))
            {
                prefetchTasks.Add(failedStream.WholeMediaPrefetchTask);
                await firstFetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                allowFirstFetchFailure.TrySetResult(true);
                await failedStream.WholeMediaPrefetchTask.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal(1, fetchAttempts);

                Assert.Equal(0, failedStream.Position);
                var buffer = await ReadAllAsync(failedStream, expectedBytes.Length);
                Assert.Equal(expectedBytes, buffer);
            }

            await using (var cachedStream = new NebulaChunkedStream(
                null,
                parts,
                totalSize,
                NullLogger.Instance,
                cacheAccessor,
                "mongo:" + persistedMediaId,
                acquirePlaybackLease: true,
                FetchChunk))
            {
                prefetchTasks.Add(cachedStream.WholeMediaPrefetchTask);
                await cachedStream.WholeMediaPrefetchTask.WaitAsync(TimeSpan.FromSeconds(10));
                var cachedBytes = await ReadAllAsync(cachedStream, expectedBytes.Length);
                Assert.Equal(expectedBytes, cachedBytes);
            }

            Assert.Equal(3, fetchAttempts);
            Assert.Equal(1, cache.TelegramFetchFailures);
            Assert.Equal(4, cache.CacheHits);
        }
        finally
        {
            cache.Dispose();
            try
            {
                await Task.WhenAll(prefetchTasks).WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception) when (prefetchTasks.TrueForAll(static task => task.IsCompleted))
            {
                // Expected simulated fetch failures and cancellation are observed above; finish cleanup only after tasks stop.
            }

            if (Directory.Exists(cacheRoot))
            {
                Directory.Delete(cacheRoot, recursive: true);
            }
        }
    }

    private NebulaMongoContext CreateContext()
        => new(_connectionString, _databaseName, NullLogger<NebulaMongoContext>.Instance);

    private static async Task<byte[]> ReadAllAsync(Stream stream, int length)
    {
        var buffer = new byte[length];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read)).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        Assert.Equal(length, read);
        return buffer;
    }

    public void Dispose()
    {
        _client?.DropDatabase(_databaseName);
    }
}
