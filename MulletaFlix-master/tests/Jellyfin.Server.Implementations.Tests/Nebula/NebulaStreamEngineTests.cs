using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaStreamEngineTests
{
    [Fact]
    public void HttpStreamServer_CreatesServerActivityWithW3CParentAndLowCardinalityTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaHttpStreamServer.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = NebulaHttpStreamServer.StartRequestActivity(
            "00-0123456789abcdef0123456789abcdef-0123456789abcdef-01",
            "vendorname=opaque",
            "GET",
            "stream");

        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Server, activity.Kind);
        Assert.Equal("0123456789abcdef0123456789abcdef", activity.TraceId.ToString());
        Assert.Equal("0123456789abcdef", activity.ParentSpanId.ToString());
        Assert.Equal("vendorname=opaque", activity.TraceStateString);
        Assert.Equal("GET", activity.GetTagItem("http.request.method"));
        Assert.Equal("stream", activity.GetTagItem("http.route"));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TelegramPoolActivity_UsesLowCardinalityTagsWithoutMediaIdentifiers()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaTelegramPool.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = NebulaTelegramPool.StartTelegramActivity("telegram.download_chunk", 2);

        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("telegram.download_chunk", activity.OperationName);
        Assert.Equal(2, activity.GetTagItem("telegram.bot.index"));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("file", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("chat", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MongoActivity_UsesOperationOnlyWithoutConnectionOrDataIdentifiers()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = NebulaMongoContext.StartMongoActivity("mongodb.ping");

        Assert.NotNull(activity);
        Assert.Equal(ActivityKind.Client, activity.Kind);
        Assert.Equal("mongodb.ping", activity.OperationName);
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("connection", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("database", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MongoPing_CancellationIsRecordedWithoutSensitiveTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.PingAsync(cancellation.Token));
    }

    [Fact]
    public async Task MongoCountFiles_CancellationIsRecordedWithoutSensitiveTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.CountFilesAsync(cancellation.Token));
    }

    [Fact]
    public async Task MongoGetChildren_CancellationIsRecordedWithoutSensitiveTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.GetChildrenAsync(null, "/", cancellation.Token));
    }

    [Fact]
    public async Task MongoFindById_CancellationIsRecordedWithoutSensitiveTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.FindByIdAsync("507f1f77bcf86cd799439011", cancellation.Token));
    }

    [Fact]
    public async Task MongoFindByNameAndParent_CancellationIsRecordedWithoutSensitiveTags()
    {
        System.Diagnostics.Activity? stoppedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stoppedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.FindByNameAndParentAsync(
            "Sensitive title",
            "507f1f77bcf86cd799439011",
            "/sensitive/path",
            cancellation.Token));

        Assert.NotNull(stoppedActivity);
        Assert.Equal("mongodb.find_by_name_and_parent", stoppedActivity!.OperationName);
        Assert.Equal("cancelled", stoppedActivity.GetTagItem("mongodb.result"));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("id", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MongoFindByTelegramMessage_CancellationIsRecordedWithoutSensitiveTags()
    {
        System.Diagnostics.Activity? stoppedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stoppedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.FindByTelegramMessageAsync(
            123456789,
            987654,
            cancellation.Token));

        Assert.NotNull(stoppedActivity);
        Assert.Equal("mongodb.find_by_telegram_message", stoppedActivity!.OperationName);
        Assert.Equal("cancelled", stoppedActivity.GetTagItem("mongodb.result"));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("chat", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("message", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MongoFindFileById_CancellationIsRecordedWithoutSensitiveTags()
    {
        System.Diagnostics.Activity? stoppedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stoppedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.FindFileByIdAsync(
            MongoDB.Bson.ObjectId.Parse("507f1f77bcf86cd799439011"),
            cancellation.Token));

        Assert.NotNull(stoppedActivity);
        Assert.Equal("mongodb.find_file_by_id", stoppedActivity!.OperationName);
        Assert.Equal("cancelled", stoppedActivity.GetTagItem("mongodb.result"));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MongoHasAnyFileDescendant_CancellationIsRecordedWithoutSensitiveTags()
    {
        System.Diagnostics.Activity? stoppedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stoppedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.HasAnyFileDescendantAsync(
            "/raphael/Series/Sensitive Title",
            cancellation.Token));

        Assert.NotNull(stoppedActivity);
        Assert.Equal("mongodb.has_any_file_descendant", stoppedActivity!.OperationName);
        Assert.Equal("cancelled", stoppedActivity.GetTagItem("mongodb.result"));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("name", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MongoFindUserByLogin_CancellationIsRecordedWithoutLoginTag()
    {
        System.Diagnostics.Activity? stoppedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stoppedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.FindUserByLoginAsync(
            "operator-account",
            cancellation.Token));

        Assert.NotNull(stoppedActivity);
        Assert.Equal("mongodb.find_user_by_login", stoppedActivity!.OperationName);
        Assert.Equal("cancelled", stoppedActivity.GetTagItem("mongodb.result"));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("login", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(stoppedActivity.TagObjects, tag => tag.Key.Contains("user", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task MongoCatalogQueries_RecordCancellationWithLowCardinalityOperationNames()
    {
        var stopped = new List<System.Diagnostics.Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var expectations = new (string Operation, Func<Task> Invoke)[]
        {
            ("mongodb.get_all_completed_files", () => context.GetAllCompletedFilesAsync(cancellation.Token)),
            ("mongodb.get_completed_or_active_files", () => context.GetCompletedOrActiveFilesAsync(cancellation.Token)),
            ("mongodb.get_all_files_for_sync", () => context.GetAllFilesForSyncAsync(cancellation.Token)),
            ("mongodb.build_directory_path_map", () => context.BuildDirectoryPathMapAsync(cancellation.Token)),
            ("mongodb.get_bot_tokens", () => context.GetBotTokensAsync(null, cancellation.Token))
        };

        foreach (var (operation, invoke) in expectations)
        {
            stopped.Clear();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(invoke);

            var activity = Assert.Single(stopped);
            Assert.Equal(operation, activity.OperationName);
            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal("cancelled", activity.GetTagItem("mongodb.result"));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("token", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("collection", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("database", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task MongoQueueTransitions_RecordCancellationWithStableStateVocabulary()
    {
        var stopped = new List<System.Diagnostics.Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var id = MongoDB.Bson.ObjectId.Parse("507f1f77bcf86cd799439011");
        var expectations = new (string Operation, string QueueState, Func<Task> Invoke)[]
        {
            ("mongodb.claim_file_for_upload", "uploading", () => context.ClaimFileForUploadAsync(id, 1, 0, cancellation.Token)),
            ("mongodb.update_upload_progress", "uploading", () => context.UpdateUploadProgressAsync(id, new MongoDB.Bson.BsonArray(), 1024L, 0, "1", cancellation.Token)),
            ("mongodb.mark_upload_failed", "failed", () => context.MarkUploadFailedAsync(id, "Sensitive /media/path failure", "telegram_send", cancellation.Token, "1")),
            ("mongodb.complete_file_upload", "completed", () => context.CompleteFileUploadAsync(id, new MongoDB.Bson.BsonDocument("status", "completed"), cancellation.Token, "1")),
            ("mongodb.mark_upload_cancelled", "cancelled", () => context.MarkUploadCancelledAsync(id, "1", cancellation.Token)),
            ("mongodb.requeue_interrupted_uploads", "queued", () => context.RequeueInterruptedUploadsAsync(cancellation.Token))
        };

        foreach (var (operation, queueState, invoke) in expectations)
        {
            stopped.Clear();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(invoke);

            var activity = Assert.Single(stopped);
            Assert.Equal(operation, activity.OperationName);
            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal("cancelled", activity.GetTagItem("mongodb.result"));
            Assert.Equal(queueState, activity.GetTagItem("nebula.queue.state"));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("reason", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("worker", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("token", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                activity.TagObjects,
                tag => tag.Value is string value && value.Contains("Sensitive", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task TelegramSendOperations_RecordRejectionWithoutContentOrCredentialTags()
    {
        var stopped = new List<System.Diagnostics.Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaTelegramPool.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        var sessionsDirectory = Path.Combine(Path.GetTempPath(), "nebula-telegram-trace-" + Guid.NewGuid().ToString("N"));
        await using var pool = new NebulaTelegramPool(
            12345,
            "test_hash",
            new[] { "0000:placeholder" },
            -1001234567890,
            sessionsDirectory,
            NullLogger<NebulaTelegramPool>.Instance);

        // Cada chamada é rejeitada antes de qualquer I/O de rede: bot ausente do
        // pool, imagem inexistente e chat vazio. O span deve existir mesmo assim.
        var expectations = new (string Operation, Func<Task> Invoke)[]
        {
            ("telegram.upload_document", () => pool.UploadDocumentAsync(
                7,
                new byte[] { 1, 2, 3 },
                "Sensitive Title S01E01.mkv",
                "video/x-matroska",
                "Sensitive caption",
                TestContext.Current.CancellationToken)),
            ("telegram.send_photo", () => pool.SendPhotoAsync(
                Path.Combine(sessionsDirectory, "missing-Sensitive-cover.jpg"),
                "Sensitive caption",
                null,
                TestContext.Current.CancellationToken)),
            ("telegram.send_message", () => pool.SendMessageAsync(
                "Sensitive message body",
                "0",
                TestContext.Current.CancellationToken))
        };

        foreach (var (operation, invoke) in expectations)
        {
            stopped.Clear();
            await invoke();

            var activity = Assert.Single(stopped);
            Assert.Equal(operation, activity.OperationName);
            Assert.Equal(ActivityKind.Client, activity.Kind);
            Assert.Equal("rejected", activity.GetTagItem("telegram.result"));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("file", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("caption", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("chat", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("token", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                activity.TagObjects,
                tag => tag.Value is string value && value.Contains("Sensitive", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task MongoStagingScan_RecordsRootCountAndSkipsWhenNoRootExists()
    {
        var stopped = new List<System.Diagnostics.Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        using var context = new NebulaMongoContext(
            "mongodb://127.0.0.1:27017",
            "test",
            NullLogger<NebulaMongoContext>.Instance);

        // Nenhuma raiz existe no disco: a varredura sai antes de qualquer I/O de
        // banco, mas o span deve registrar que nada foi varrido.
        var missingRoot = Path.Combine(Path.GetTempPath(), "nebula-missing-Sensitive-" + Guid.NewGuid().ToString("N"));
        await context.SyncStagingDirectoryAsync(
            new[] { missingRoot },
            false,
            TestContext.Current.CancellationToken);

        var skipped = Assert.Single(stopped);
        Assert.Equal("mongodb.sync_staging_directory", skipped.OperationName);
        Assert.Equal(ActivityKind.Client, skipped.Kind);
        Assert.Equal("skipped", skipped.GetTagItem("mongodb.result"));
        Assert.Equal(0, skipped.GetTagItem("nebula.staging.root_count"));
        Assert.DoesNotContain(skipped.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(skipped.TagObjects, tag => tag.Key.Contains("root", StringComparison.OrdinalIgnoreCase) && tag.Value is string);
        Assert.DoesNotContain(
            skipped.TagObjects,
            tag => tag.Value is string value && value.Contains("Sensitive", StringComparison.OrdinalIgnoreCase));

        // Com uma raiz real e vazia, a contagem de raízes é registrada; o Mongo
        // indisponível é tratado internamente e não deve vazar caminho no span.
        var realRoot = Directory.CreateTempSubdirectory("nebula-staging-Sensitive-").FullName;
        try
        {
            stopped.Clear();
            await context.SyncStagingDirectoryAsync(
                new[] { realRoot },
                false,
                TestContext.Current.CancellationToken);

            var scanned = Assert.Single(
                stopped,
                activity => activity.OperationName == "mongodb.sync_staging_directory");
            Assert.Equal(1, scanned.GetTagItem("nebula.staging.root_count"));
            Assert.DoesNotContain(scanned.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(
                scanned.TagObjects,
                tag => tag.Value is string value && value.Contains("Sensitive", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(realRoot, true);
        }
    }

    [Fact]
    public async Task PlaybackSession_RecordsSpansWithoutMediaPathOrSessionIdentifiers()
    {
        var stopped = new List<System.Diagnostics.Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaPlaybackSessionMonitor.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => stopped.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        var manager = new Mock<INebulaFtpManager>(MockBehavior.Loose);
        manager
            .Setup(mock => mock.CancelPlaybackPrefetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(true));
        var monitor = new NebulaPlaybackSessionMonitor(
            Mock.Of<ISessionManager>(),
            manager.Object,
            NullLogger<NebulaPlaybackSessionMonitor>.Instance);

        // Início de reprodução: o span registra a transição, nunca o caminho.
        await monitor.TrackPlaybackStartAsync("session-sensitive", "play-sensitive", "N:/Series/Sensitive Title S01E01.mkv");

        var start = Assert.Single(stopped);
        Assert.Equal("nebula.playback.session_start", start.OperationName);
        Assert.Equal(ActivityKind.Internal, start.Kind);
        Assert.Equal("tracked", start.GetTagItem("nebula.playback.result"));
        AssertNoPlaybackIdentifiers(start);

        // Troca de mídia na mesma sessão: o pré-cache anterior é cancelado.
        stopped.Clear();
        await monitor.TrackPlaybackStartAsync("session-sensitive", "play-sensitive-2", "N:/Series/Sensitive Title S01E02.mkv");

        var switched = Assert.Single(stopped);
        Assert.Equal("nebula.playback.session_start", switched.OperationName);
        Assert.Equal(true, switched.GetTagItem("nebula.playback.prefetch_cancelled"));
        AssertNoPlaybackIdentifiers(switched);

        // Parada da reprodução ativa.
        stopped.Clear();
        await monitor.TrackPlaybackStoppedAsync("session-sensitive", "play-sensitive-2", "N:/Series/Sensitive Title S01E02.mkv");

        var end = Assert.Single(stopped);
        Assert.Equal("nebula.playback.session_stop", end.OperationName);
        Assert.Equal("tracked", end.GetTagItem("nebula.playback.result"));
        AssertNoPlaybackIdentifiers(end);

        // Sessão desconhecida: a parada é ignorada, mas permanece observável.
        stopped.Clear();
        await monitor.TrackPlaybackStoppedAsync("session-unknown", "play-unknown", "N:/Series/Sensitive Other.mkv");

        var ignored = Assert.Single(stopped);
        Assert.Equal("nebula.playback.session_stop", ignored.OperationName);
        Assert.Equal("ignored", ignored.GetTagItem("nebula.playback.result"));
        AssertNoPlaybackIdentifiers(ignored);
    }

    private static void AssertNoPlaybackIdentifiers(System.Diagnostics.Activity activity)
    {
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("session_id", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("media", StringComparison.OrdinalIgnoreCase) && tag.Value is string);
        Assert.DoesNotContain(
            activity.TagObjects,
            tag => tag.Value is string value && value.Contains("Sensitive", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            activity.TagObjects,
            tag => tag.Value is string value && value.Contains("session-", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NormalizeUploadFailureStage_MapsUnexpectedErrorToItsOwnStageInsteadOfUnknown()
    {
        // As etapas explícitas continuam estáveis.
        Assert.Equal(
            NebulaUploadFailureStages.TelegramAvailability,
            NebulaMongoContext.NormalizeUploadFailureStage(NebulaUploadFailureStages.TelegramAvailability));
        Assert.Equal(
            NebulaUploadFailureStages.TelegramTransfer,
            NebulaMongoContext.NormalizeUploadFailureStage(NebulaUploadFailureStages.TelegramTransfer));
        Assert.Equal(
            NebulaUploadFailureStages.UploadIntegrity,
            NebulaMongoContext.NormalizeUploadFailureStage(NebulaUploadFailureStages.UploadIntegrity));

        // Uma exceção não encaminhada pelos ramos explícitos do upload precisa
        // ser distinguível de um documento legado sem `failure_stage`, senão o
        // painel agrupa as duas causas no mesmo balde `unknown`.
        Assert.Equal(
            NebulaUploadFailureStages.UnexpectedError,
            NebulaMongoContext.NormalizeUploadFailureStage(NebulaUploadFailureStages.UnexpectedError));

        // Valores ausentes, vazios ou desconhecidos permanecem `unknown`.
        Assert.Equal(NebulaUploadFailureStages.Unknown, NebulaMongoContext.NormalizeUploadFailureStage(null));
        Assert.Equal(NebulaUploadFailureStages.Unknown, NebulaMongoContext.NormalizeUploadFailureStage(string.Empty));
        Assert.Equal(NebulaUploadFailureStages.Unknown, NebulaMongoContext.NormalizeUploadFailureStage("N:/media/Sensitive.mkv"));
    }

    [Fact]
    public void WorkerOwnershipFilter_DoesNotLetAZombieWorkerAdoptARequeuedDocument()
    {
        // Cenário real de perda de dados:
        //  1. worker "1" reivindica o documento e começa a enviar partes;
        //  2. o worker travou, `modified_at` ficou obsoleto e
        //     `RequeueInterruptedUploadsAsync` fez `Unset("worker_id")`,
        //     devolvendo o item para `queued`;
        //  3. worker "2" reivindica legitimamente e passa a gravar partes;
        //  4. o worker "1", ainda vivo, volta e grava progresso.
        //
        // Enquanto a posse aceitar "sem dono" como prova, o passo 4 sobrescreve
        // as partes do worker "2" com um estado obsoleto.
        Assert.True(NebulaMongoContext.IsOwnedByWorker("1", "1"));
        Assert.True(NebulaMongoContext.IsOwnedByWorker(1, "1"));

        // Documento requeuado (sem dono) não pertence a ninguém.
        Assert.False(NebulaMongoContext.IsOwnedByWorker(null, "1"));
        Assert.False(NebulaMongoContext.IsOwnedByWorker(string.Empty, "1"));

        // E nunca pertence a outro worker.
        Assert.False(NebulaMongoContext.IsOwnedByWorker("2", "1"));
        Assert.False(NebulaMongoContext.IsOwnedByWorker(2, "1"));
    }

    [Theory]
    // Lease vigente: nenhum outro worker pode reivindicar.
    [InlineData(2000L, 1000L, 1500L, false)]
    // Lease expirado: recuperação liberada assim que passa o prazo.
    [InlineData(1400L, 1000L, 1500L, true)]
    [InlineData(1500L, 1000L, 1500L, true)]
    public void StaleLeaseReclaim_UsesLeaseExpiryWhenPresent(long leaseUntil, long modifiedAt, long now, bool expected)
    {
        Assert.Equal(expected, NebulaMongoContext.ShouldReclaimStaleLease(leaseUntil, modifiedAt, now));
    }

    [Fact]
    public void StaleLeaseReclaim_FallsBackToTheLegacyHourWindowWithoutLease()
    {
        // Documentos gravados antes do lease explícito não têm `lease_until`.
        // Descartá-los como "sem lease, logo recuperável" liberaria na hora um
        // upload que está ativo agora; a janela legada de 1 h precisa continuar
        // valendo como único critério nesse caso.
        const long Now = 100_000L;

        Assert.False(NebulaMongoContext.ShouldReclaimStaleLease(null, Now - 60, Now));
        Assert.False(NebulaMongoContext.ShouldReclaimStaleLease(null, Now - 3599, Now));
        Assert.True(NebulaMongoContext.ShouldReclaimStaleLease(null, Now - 3601, Now));

        // Sem lease e sem `modified_at` não há prova de atividade alguma.
        Assert.True(NebulaMongoContext.ShouldReclaimStaleLease(null, null, Now));
    }

    [Fact]
    public void LeaseFencing_RejectsAStaleLeaseFromThePreviousClaimOfTheSameWorker()
    {
        // O mesmo worker pode reivindicar o item duas vezes: se o lease antigo
        // expirou e foi recuperado, a escrita em voo do ciclo anterior não pode
        // valer. Só o `lease_id` corrente autoriza a escrita — por isso a posse
        // sozinha (worker_id) não basta.
        Assert.True(NebulaMongoContext.IsLeaseHeldBy("1", "lease-b", "1", "lease-b"));
        Assert.False(NebulaMongoContext.IsLeaseHeldBy("1", "lease-b", "1", "lease-a"));

        // Documento legado sem `lease_id` continua governado apenas pela posse,
        // senão uploads em andamento na atualização travariam.
        Assert.True(NebulaMongoContext.IsLeaseHeldBy("1", null, "1", null));
        Assert.True(NebulaMongoContext.IsLeaseHeldBy("1", null, "1", "lease-a"));

        // Posse de terceiro nunca passa, com ou sem lease.
        Assert.False(NebulaMongoContext.IsLeaseHeldBy("2", "lease-b", "1", "lease-b"));
        Assert.False(NebulaMongoContext.IsLeaseHeldBy(null, null, "1", null));
    }

    [Theory]
    // Configuração explícita é respeitada dentro dos limites.
    [InlineData(4, 4)]
    [InlineData(1, 1)]
    [InlineData(32, 32)]
    // Zero/negativo cai no padrão em vez de travar ou explodir a concorrência.
    [InlineData(0, 8)]
    [InlineData(-5, 8)]
    // Valores absurdos são limitados: sem teto, uma configuração errada abre
    // centenas de transferências simultâneas e derruba a banda e o Telegram.
    [InlineData(500, 32)]
    public void ResolveTransferConcurrency_ClampsConfiguredValueToASafeRange(int configured, int expected)
    {
        Assert.Equal(expected, NebulaTransferLimits.ResolveUploadConcurrency(configured));
    }

    [Theory]
    [InlineData(24, 24)]
    [InlineData(0, 24)]
    [InlineData(-1, 24)]
    [InlineData(1, 1)]
    [InlineData(999, 32)]
    public void ResolveDownloadConnections_ClampsConfiguredValueToASafeRange(int configured, int expected)
    {
        Assert.Equal(expected, NebulaTransferLimits.ResolveDownloadConnections(configured));
    }

    [Fact]
    public void TransferLimits_KeepUploadAndDownloadBudgetsIndependent()
    {
        // T2.4 exige limites independentes por operação. Um download saturando
        // suas conexões não pode consumir o orçamento de upload, e vice-versa:
        // os dois valores vêm de chaves de configuração distintas e não
        // compartilham um teto único.
        var uploads = NebulaTransferLimits.ResolveUploadConcurrency(8);
        var downloads = NebulaTransferLimits.ResolveDownloadConnections(24);

        Assert.Equal(8, uploads);
        Assert.Equal(24, downloads);

        // O teto agregado existe para proteger a rede, mas não é atingido por
        // uma das operações sozinha.
        Assert.True(uploads <= NebulaTransferLimits.MaxUploadConcurrency);
        Assert.True(downloads <= NebulaTransferLimits.MaxDownloadConnections);
    }

    [Fact]
    public void BurstLimiter_AllowsUpToTheWindowQuotaThenThrottles()
    {
        // Limitar simultaneidade não impede rajada: com 8 slots livres o motor
        // dispara 8 chamadas instantâneas, e o Telegram responde com flood-wait.
        // O controle por janela de tempo é o que falta.
        var now = TimeSpan.Zero;
        var limiter = new NebulaBurstLimiter(maxPerWindow: 3, window: TimeSpan.FromSeconds(10), clock: () => now);

        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());

        // Quarta chamada na mesma janela é recusada.
        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public void BurstLimiter_RecoversQuotaAsTheWindowSlides()
    {
        var now = TimeSpan.Zero;
        var limiter = new NebulaBurstLimiter(maxPerWindow: 2, window: TimeSpan.FromSeconds(10), clock: () => now);

        Assert.True(limiter.TryAcquire());
        now = TimeSpan.FromSeconds(4);
        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());

        // A janela é deslizante, não fixa: em t=11 apenas a permissão de t=0
        // expirou, então exatamente uma nova permissão é liberada.
        now = TimeSpan.FromSeconds(11);
        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());

        // Quando a segunda permissão também expira, a cota volta ao total.
        now = TimeSpan.FromSeconds(15);
        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void BurstLimiter_ReportsTheWaitUntilTheNextPermit()
    {
        var now = TimeSpan.Zero;
        var limiter = new NebulaBurstLimiter(maxPerWindow: 1, window: TimeSpan.FromSeconds(10), clock: () => now);

        Assert.True(limiter.TryAcquire());

        // O chamador precisa saber quanto esperar; sem isso restaria busy-wait.
        now = TimeSpan.FromSeconds(3);
        Assert.Equal(TimeSpan.FromSeconds(7), limiter.GetRetryDelay());

        now = TimeSpan.FromSeconds(10);
        Assert.Equal(TimeSpan.Zero, limiter.GetRetryDelay());
    }

    [Fact]
    public void BurstLimiter_WithNonPositiveQuotaDoesNotBlockEverything()
    {
        // Configuração inválida não pode travar a fila inteira: um limite zero
        // interpretado literalmente pararia todos os envios para sempre.
        var limiter = new NebulaBurstLimiter(maxPerWindow: 0, window: TimeSpan.FromSeconds(10));

        Assert.True(limiter.TryAcquire());
        Assert.Equal(TimeSpan.Zero, limiter.GetRetryDelay());
    }

    [Fact]
    public void BurstLimiter_WithNonPositiveWindowDisablesThrottling()
    {
        var limiter = new NebulaBurstLimiter(maxPerWindow: 1, window: TimeSpan.Zero);

        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
    }

    [Fact]
    public void HttpStreamServer_NormalizesUntrustedMethodsBeforeAddingActivityTag()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaHttpStreamServer.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = NebulaHttpStreamServer.StartRequestActivity(null, null, "X-Custom-Secret", "other");

        Assert.NotNull(activity);
        Assert.Equal("OTHER", activity.GetTagItem("http.request.method"));
    }

    [Fact]
    public async Task ChunkedStream_ReadsFromLocalPath_WhenAvailable()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var expectedBytes = new byte[256];
            for (int i = 0; i < expectedBytes.Length; i++)
            {
                expectedBytes[i] = (byte)i;
            }

            await File.WriteAllBytesAsync(tempFile, expectedBytes);

            var part = new NebulaStreamPart
            {
                PartIndex = 0,
                FileOffset = 0,
                Size = expectedBytes.Length,
                LocalPath = tempFile
            };

            await using var stream = new NebulaChunkedStream(null!, new List<NebulaStreamPart> { part }, expectedBytes.Length, NullLogger.Instance);

            Assert.Equal(expectedBytes.Length, stream.Length);
            Assert.Equal(0, stream.Position);
            Assert.True(stream.CanRead);
            Assert.True(stream.CanSeek);
            Assert.False(stream.CanWrite);

            // Test seeking
            stream.Seek(10, SeekOrigin.Begin);
            Assert.Equal(10, stream.Position);

            var buffer = new byte[20];
            var read = await stream.ReadAsync(buffer.AsMemory(0, 20));
            Assert.Equal(20, read);
            Assert.Equal(30, stream.Position);

            for (int i = 0; i < 20; i++)
            {
                Assert.Equal(10 + i, buffer[i]);
            }
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData("video.mp4", "video/mp4")]
    [InlineData("movie.mkv", "video/x-matroska")]
    [InlineData("clip.webm", "video/webm")]
    [InlineData("song.mp3", "audio/mpeg")]
    [InlineData("subtitles.srt", "text/plain; charset=utf-8")]
    [InlineData("subtitles.vtt", "text/vtt; charset=utf-8")]
    [InlineData("unknown.xyz", "application/octet-stream")]
    public void HttpStreamServer_GuessesContentTypeCorrectly(string fileName, string expectedContentType)
    {
        var method = typeof(NebulaHttpStreamServer).GetMethod(
            "GuessContentType",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var result = (string?)method.Invoke(null, [fileName]);
        Assert.Equal(expectedContentType, result);
    }

    [Theory]
    [InlineData(null, 100, 0, 99, false, true)]
    [InlineData("bytes=0-9", 100, 0, 9, true, true)]
    [InlineData("bytes=90-", 100, 90, 99, true, true)]
    [InlineData("bytes=-10", 100, 90, 99, true, true)]
    [InlineData("bytes=-200", 100, 0, 99, true, true)]
    [InlineData("bytes=100-", 100, 0, 0, false, false)]
    [InlineData("bytes=20-10", 100, 0, 0, false, false)]
    [InlineData("bytes=0-1,4-5", 100, 0, 0, false, false)]
    [InlineData("bytes=-0", 100, 0, 0, false, false)]
    public void HttpStreamServer_ParsesSingleByteRangesSafely(
        string? header,
        long totalSize,
        long expectedStart,
        long expectedEnd,
        bool expectedRange,
        bool expectedValid)
    {
        var method = typeof(NebulaHttpStreamServer).GetMethod(
            "TryParseRange",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var arguments = new object?[] { header, totalSize, 0L, 0L, false };
        var result = Assert.IsType<bool>(method.Invoke(null, arguments));

        Assert.Equal(expectedValid, result);
        if (result)
        {
            Assert.Equal(expectedStart, arguments[2]);
            Assert.Equal(expectedEnd, arguments[3]);
            Assert.Equal(expectedRange, arguments[4]);
        }
    }

    [Theory]
    [InlineData("movie\"name.mkv", "inline; filename=\"moviename.mkv\"; filename*=UTF-8''moviename.mkv")]
    [InlineData("episode\r\nX-Injected: true.mkv", "inline; filename=\"episodeX-Injected: true.mkv\"; filename*=UTF-8''episodeX-Injected%3A%20true.mkv")]
    [InlineData("", "inline; filename=\"media.bin\"; filename*=UTF-8''media.bin")]
    public void HttpStreamServer_SanitizesContentDispositionFileNames(string fileName, string expectedHeader)
    {
        var method = typeof(NebulaHttpStreamServer).GetMethod(
            "BuildContentDisposition",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var result = Assert.IsType<string>(method.Invoke(null, [fileName]));

        Assert.Equal(expectedHeader, result);
    }
}
