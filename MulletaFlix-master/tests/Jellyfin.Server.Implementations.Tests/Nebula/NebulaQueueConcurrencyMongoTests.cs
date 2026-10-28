using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using WTelegram;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Exercita a fila de upload contra um MongoDB real e descartável.
/// </summary>
/// <remarks>
/// O fencing de posse e o lease explícito existem para impedir que dois workers
/// gravem no mesmo documento. Testes com mocks não provam isso: só uma corrida
/// real contra o banco mostra se o claim é atômico. Os testes são ignorados
/// quando não há uma instância local explicitamente configurada para testes.
/// </remarks>
[Trait("Category", "RequiresMongo")]
public sealed class NebulaQueueConcurrencyMongoTests : IDisposable
{
    private readonly string _databaseName = "nebula_t2_" + Guid.NewGuid().ToString("N");
    private readonly string _testConnectionString;
    private readonly bool _available;
    private readonly string _skipReason;
    private readonly MongoClient? _client;

    public NebulaQueueConcurrencyMongoTests()
    {
        if (!NebulaMongoTestConnection.TryGet(out var testConnectionString, out var skipReason))
        {
            _testConnectionString = string.Empty;
            _skipReason = skipReason;
            _available = false;
            return;
        }

        _testConnectionString = testConnectionString;
        _skipReason = string.Empty;
        try
        {
            var settings = MongoClientSettings.FromConnectionString(_testConnectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
            _client = new MongoClient(settings);
            _client.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            _available = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Falha ao validar a conexão Mongo de teste configurada em 127.0.0.1:27099.",
                ex);
        }
    }

    private NebulaMongoContext CreateContext()
        => new(_testConnectionString, _databaseName, NullLogger<NebulaMongoContext>.Instance);

    private IMongoCollection<BsonDocument> Files
        => _client!.GetDatabase(_databaseName).GetCollection<BsonDocument>("files");

    private IMongoCollection<BsonDocument> Admission
        => _client!.GetDatabase(_databaseName).GetCollection<BsonDocument>("nebula_admission");

    private async Task<ObjectId> InsertQueuedFileAsync()
    {
        var id = ObjectId.GenerateNewId();
        await Files.InsertOneAsync(new BsonDocument
        {
            { "_id", id },
            { "name", "media.mkv" },
            { "type", "file" },
            { "is_directory", false },
            { "status", "queued" },
            { "size", 1024L },
            { "parts", new BsonArray() },
            { "queued_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
            { "modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
        });
        return id;
    }

    [Fact]
    public async Task InsertFileDocIfAbsentAsync_PreservesCallerObjectIdForWorkerTransitions()
    {
        if (!_available)
        {
            Assert.Skip(_skipReason);
        }

        var expectedId = ObjectId.GenerateNewId();
        var localPath = Path.Combine(Path.GetTempPath(), $"nebula-idempotency-{Guid.NewGuid():N}.mkv");
        var initialDocument = new BsonDocument
        {
            { "_id", expectedId },
            { "name", Path.GetFileName(localPath) },
            { "type", "file" },
            { "is_directory", false },
            { "local_path", localPath },
            { "status", "queued" },
            { "parts", new BsonArray() }
        };
        var inserted = await CreateContext().InsertFileDocIfAbsentAsync(initialDocument, TestContext.Current.CancellationToken);

        Assert.True(inserted);
        var persisted = await Files.Find(Builders<BsonDocument>.Filter.Eq("local_path", localPath))
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expectedId, persisted["_id"].AsObjectId);

        var duplicateDocument = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() },
            { "name", Path.GetFileName(localPath) },
            { "type", "file" },
            { "is_directory", false },
            { "local_path", localPath },
            { "status", "queued" },
            { "parts", new BsonArray() }
        };
        var duplicate = await CreateContext().InsertFileDocIfAbsentAsync(duplicateDocument, TestContext.Current.CancellationToken);

        Assert.False(duplicate);
        persisted = await Files.Find(Builders<BsonDocument>.Filter.Eq("local_path", localPath))
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expectedId, persisted["_id"].AsObjectId);
        Assert.Equal(1, await Files.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("local_path", localPath),
            cancellationToken: TestContext.Current.CancellationToken));
        var claim = await CreateContext().ClaimFileForUploadAsync(expectedId, 1, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(claim);
        Assert.True(await CreateContext().UpdateUploadProgressAsync(
            expectedId,
            new BsonArray(),
            128,
            1,
            "1",
            TestContext.Current.CancellationToken,
            claim["lease_id"].AsString));
    }

    [Fact]
    public async Task ResetUploadGenerationAsync_RequiresCurrentLeaseAndClearsOldParts()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();
        var claim = await context.ClaimFileForUploadAsync(id, 41, 0, TestContext.Current.CancellationToken);
        Assert.NotNull(claim);
        var currentLeaseId = claim["lease_id"].AsString;
        var oldParts = new BsonArray
        {
            new BsonDocument
            {
                { "part_number", 0 },
                { "source_sha256", new string('a', 64) }
            }
        };
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("parts", oldParts).Set("uploaded_bytes", 256L),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(await context.ResetUploadGenerationAsync(id, "stale-generation", "41", "old-lease", 3, TestContext.Current.CancellationToken));
        var unchanged = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(oldParts, unchanged["parts"].AsBsonArray);
        Assert.Equal(256L, unchanged["uploaded_bytes"].ToInt64());

        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("lease_until", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(await context.ResetUploadGenerationAsync(id, "expired-generation", "41", currentLeaseId, 3, TestContext.Current.CancellationToken));
        unchanged = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(oldParts, unchanged["parts"].AsBsonArray);
        Assert.Equal(256L, unchanged["uploaded_bytes"].ToInt64());

        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("lease_until", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(await context.ResetUploadGenerationAsync(id, "new-generation", "41", currentLeaseId, 3, TestContext.Current.CancellationToken));
        var reset = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("uploading", reset["status"].AsString);
        Assert.Equal("new-generation", reset["file_uuid"].AsString);
        Assert.Equal("new-generation", reset["obfuscated_id"].AsString);
        Assert.Empty(reset["parts"].AsBsonArray);
        Assert.Equal(0L, reset["uploaded_bytes"].ToInt64());
        Assert.Equal(3, reset["part_count"].ToInt32());
        Assert.Equal(currentLeaseId, reset["lease_id"].AsString);
    }

    private async Task<(ObjectId Id, string StaleLeaseId, string CurrentLeaseId)> ReclaimForSameWorkerAsync(NebulaMongoContext context)
    {
        var id = await InsertQueuedFileAsync();
        var firstClaim = await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None);
        Assert.NotNull(firstClaim);
        var staleLeaseId = firstClaim["lease_id"].AsString;

        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("lease_until", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10),
            cancellationToken: TestContext.Current.CancellationToken);

        var secondClaim = await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None);
        Assert.NotNull(secondClaim);
        var currentLeaseId = secondClaim["lease_id"].AsString;
        Assert.NotEqual(staleLeaseId, currentLeaseId);

        return (id, staleLeaseId, currentLeaseId);
    }

    [Fact]
    public async Task UploadQueueSummary_RecordsSuccessfulMongoActivity()
    {
        Assert.SkipUnless(_available, _skipReason);

        var stopped = new List<System.Diagnostics.Activity>();
        using var listener = new System.Diagnostics.ActivityListener
        {
            ShouldListenTo = source => source.Name == NebulaMongoContext.ActivitySourceName,
            Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> options) =>
                options.Name == "mongodb.get_upload_queue_summary"
                    ? System.Diagnostics.ActivitySamplingResult.AllData
                    : System.Diagnostics.ActivitySamplingResult.None,
            ActivityStopped = activity => stopped.Add(activity)
        };
        System.Diagnostics.ActivitySource.AddActivityListener(listener);

        using var context = CreateContext();
        var summary = await context.GetUploadQueueSummaryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, summary.PendingCount);
        var activity = Assert.Single(stopped);
        Assert.Equal("mongodb.get_upload_queue_summary", activity.OperationName);
        Assert.Equal(System.Diagnostics.ActivityKind.Client, activity.Kind);
        Assert.Equal("success", activity.GetTagItem("mongodb.result"));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("name", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key.Contains("path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ConcurrentClaims_OnlyOneWorkerWinsTheDocument()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();

        // 16 workers competem pelo mesmo documento ao mesmo tempo.
        var barrier = new Barrier(16);
        var tasks = Enumerable.Range(1, 16).Select(worker => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            var claimed = await context.ClaimFileForUploadAsync(id, worker, 0, CancellationToken.None);
            return claimed is null ? null : worker.ToString(System.Globalization.CultureInfo.InvariantCulture);
        })).ToArray();

        var winners = (await Task.WhenAll(tasks)).Where(w => w is not null).ToList();

        // Exatamente um claim pode vencer: o restante encontra o documento em
        // `uploading` com lease vigente e é rejeitado pelo filtro.
        Assert.Single(winners);

        var document = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("uploading", document["status"].AsString);
        Assert.Equal(winners[0], document["worker_id"].AsString);
        Assert.True(document.Contains("lease_id"), "O claim deve gravar lease_id.");
        Assert.True(document.Contains("lease_until"), "O claim deve gravar lease_until.");
    }

    [Fact]
    public async Task ProgressWrite_FromANonOwnerWorkerIsRejected()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();

        var claimed = await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None);
        Assert.NotNull(claimed);
        var leaseId = claimed["lease_id"].AsString;

        var ownerParts = new BsonArray { new BsonDocument { { "part", 0 }, { "size", 512L } } };
        var ownerWrote = await context.UpdateUploadProgressAsync(id, ownerParts, 512, 0, "1", CancellationToken.None, leaseId);
        Assert.True(ownerWrote, "O dono do lease deve conseguir gravar progresso.");

        // Worker 2 nunca reivindicou este documento.
        var intruderParts = new BsonArray { new BsonDocument { { "part", 99 }, { "size", 1L } } };
        var intruderWrote = await context.UpdateUploadProgressAsync(id, intruderParts, 1, 0, "2", CancellationToken.None, leaseId);
        Assert.False(intruderWrote, "Worker sem posse não pode gravar progresso.");

        // E as partes do dono continuam intactas.
        var document = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("1", document["worker_id"].AsString);
        Assert.Equal(512L, document["uploaded_bytes"].ToInt64());
        Assert.Single(document["parts"].AsBsonArray);
        Assert.Equal(0, document["parts"].AsBsonArray[0].AsBsonDocument["part"].ToInt32());
    }

    [Fact]
    public async Task ReclaimedLease_FencesEveryMutationWhenWorkerIdIsReused()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var progress = await ReclaimForSameWorkerAsync(context);
        var currentParts = new BsonArray { new BsonDocument { { "part", 0 }, { "size", 900L } } };
        var staleParts = new BsonArray { new BsonDocument { { "part", 0 }, { "size", 100L } } };

        Assert.False(await context.TryAcquireUploadOwnershipAsync(progress.Id, "1", progress.StaleLeaseId));
        Assert.True(await context.TryAcquireUploadOwnershipAsync(progress.Id, "1", progress.CurrentLeaseId));
        Assert.False(await context.UpdateUploadProgressAsync(progress.Id, staleParts, 100, 0, "1", CancellationToken.None, progress.StaleLeaseId));
        Assert.True(await context.UpdateUploadProgressAsync(progress.Id, currentParts, 900, 0, "1", CancellationToken.None, progress.CurrentLeaseId));
        Assert.False(await context.MarkUploadFailedAsync(progress.Id, "stale failure", NebulaUploadFailureStages.TelegramTransfer, CancellationToken.None, "1", progress.StaleLeaseId));
        Assert.False(await context.CompleteFileUploadAsync(progress.Id, new BsonDocument { { "status", "completed" }, { "size", 100L } }, CancellationToken.None, "1", progress.StaleLeaseId));
        Assert.False(await context.CompleteDuplicateUploadAsync(progress.Id, new BsonDocument("status", "completed"), 100, CancellationToken.None, "1", progress.StaleLeaseId));
        Assert.False(await context.ReleaseUploadClaimForRetryAsync(progress.Id, 1, CancellationToken.None, progress.StaleLeaseId));

        var progressDocument = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", progress.Id)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal(900L, progressDocument["uploaded_bytes"].ToInt64());
        Assert.Equal(progress.CurrentLeaseId, progressDocument["lease_id"].AsString);

        var failed = await ReclaimForSameWorkerAsync(context);
        Assert.False(await context.MarkUploadFailedAsync(failed.Id, "stale failure", NebulaUploadFailureStages.TelegramTransfer, CancellationToken.None, "1", failed.StaleLeaseId));
        Assert.True(await context.MarkUploadFailedAsync(failed.Id, "current failure", NebulaUploadFailureStages.TelegramTransfer, CancellationToken.None, "1", failed.CurrentLeaseId));

        var completed = await ReclaimForSameWorkerAsync(context);
        var finalFields = new BsonDocument { { "status", "completed" }, { "size", 1024L } };
        Assert.False(await context.CompleteFileUploadAsync(completed.Id, finalFields, CancellationToken.None, "1", completed.StaleLeaseId));
        Assert.True(await context.CompleteFileUploadAsync(completed.Id, finalFields, CancellationToken.None, "1", completed.CurrentLeaseId));

        var duplicate = await ReclaimForSameWorkerAsync(context);
        var canonicalMedia = new BsonDocument
        {
            { "status", "completed" },
            { "parts", new BsonArray { new BsonDocument { { "part", 0 }, { "size", 1024L } } } },
            { "tg_file_id", "canonical-file" }
        };
        Assert.False(await context.CompleteDuplicateUploadAsync(duplicate.Id, canonicalMedia, 1024, CancellationToken.None, "1", duplicate.StaleLeaseId));
        Assert.True(await context.CompleteDuplicateUploadAsync(duplicate.Id, canonicalMedia, 1024, CancellationToken.None, "1", duplicate.CurrentLeaseId));

        var cancelled = await ReclaimForSameWorkerAsync(context);
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", cancelled.Id),
            Builders<BsonDocument>.Update.Set("cancel_requested_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(await context.IsUploadCancellationRequestedAsync(cancelled.Id, "1", CancellationToken.None, cancelled.StaleLeaseId));
        Assert.False(await context.MarkUploadCancelledAsync(cancelled.Id, "1", CancellationToken.None, cancelled.StaleLeaseId));
        Assert.True(await context.IsUploadCancellationRequestedAsync(cancelled.Id, "1", CancellationToken.None, cancelled.CurrentLeaseId));
        Assert.True(await context.MarkUploadCancelledAsync(cancelled.Id, "1", CancellationToken.None, cancelled.CurrentLeaseId));

        var released = await ReclaimForSameWorkerAsync(context);
        Assert.False(await context.ReleaseUploadClaimForRetryAsync(released.Id, 1, CancellationToken.None, released.StaleLeaseId));
        Assert.True(await context.ReleaseUploadClaimForRetryAsync(released.Id, 1, CancellationToken.None, released.CurrentLeaseId));
    }

    [Fact]
    public async Task RequeuedDocument_CannotBeWrittenByTheZombieWorker()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();

        // Worker 1 reivindica e grava progresso.
        var claimed = await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None);
        Assert.NotNull(claimed);
        Assert.True(await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 512L } } },
            512,
            0,
            "1",
            CancellationToken.None,
            claimed["lease_id"].AsString));

        // O worker travou e a recuperação devolveu o item à fila.
        await context.RequeueInterruptedUploadsAsync(CancellationToken.None);
        var requeued = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);

        // Só é requeuado se o lease tiver expirado; para o teste, força a expiração.
        if (requeued["status"].AsString != "queued")
        {
            await Files.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.Set("lease_until", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10),
                cancellationToken: TestContext.Current.CancellationToken);
            await context.RequeueInterruptedUploadsAsync(CancellationToken.None);
            requeued = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal("queued", requeued["status"].AsString);
        Assert.False(requeued.Contains("worker_id"), "O requeue deve remover a posse.");
        Assert.False(requeued.Contains("lease_id"), "O requeue deve limpar o lease.");

        // Worker 2 assume legitimamente.
        var secondClaim = await context.ClaimFileForUploadAsync(id, 2, 0, CancellationToken.None);
        Assert.NotNull(secondClaim);
        Assert.True(await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 900L } } },
            900,
            0,
            "2",
            CancellationToken.None,
            secondClaim["lease_id"].AsString));

        // O worker 1 volta do limbo. Esta é a corrida que corrompia os dados:
        // antes do fencing, "sem dono" o qualificava e ele sobrescrevia o
        // progresso do worker 2.
        var zombieWrote = await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 512L } } },
            512,
            0,
            "1",
            CancellationToken.None,
            claimed["lease_id"].AsString);

        Assert.False(zombieWrote, "Worker zumbi não pode readotar documento já reivindicado.");

        var final = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("2", final["worker_id"].AsString);
        Assert.Equal(900L, final["uploaded_bytes"].ToInt64());
    }

    [Fact]
    public async Task CompletedUpload_IsNotReclaimedByANewClaim()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();

        var claimed = await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None);
        Assert.NotNull(claimed);
        var completed = await context.CompleteFileUploadAsync(
            id,
            new BsonDocument
            {
                { "status", "completed" },
                { "size", 1024L },
                { "parts", new BsonArray { new BsonDocument { { "part", 0 }, { "size", 1024L } } } }
            },
            CancellationToken.None,
            "1",
            claimed["lease_id"].AsString);
        Assert.True(completed);

        // Mídia concluída nunca volta para a fila: é o requisito de idempotência
        // de T2.2 (não duplicar arquivos já enviados).
        var reclaim = await context.ClaimFileForUploadAsync(id, 2, 0, CancellationToken.None);
        Assert.Null(reclaim);

        var document = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("completed", document["status"].AsString);
        Assert.False(document.Contains("lease_id"), "A conclusão deve limpar o lease.");
    }

    [Fact]
    public async Task FailedUpload_HonoursRetryAfterBeforeBeingClaimedAgain()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();

        var claimed = await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None);
        Assert.NotNull(claimed);
        Assert.True(await context.MarkUploadFailedAsync(
            id,
            "falha transitória",
            NebulaUploadFailureStages.TelegramTransfer,
            CancellationToken.None,
            "1",
            claimed["lease_id"].AsString));

        var failed = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("failed", failed["status"].AsString);
        Assert.True(failed.Contains("retry_after"), "Falha transitória deve agendar retry.");

        // Enquanto o backoff não vence, nenhum worker pode reivindicar.
        Assert.Null(await context.ClaimFileForUploadAsync(id, 2, 0, CancellationToken.None));

        // Vencido o backoff, o item volta a ser elegível.
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("retry_after", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(await context.ClaimFileForUploadAsync(id, 2, 0, CancellationToken.None));
    }

    [Fact]
    public async Task PriorityPersistedInMongo_SurvivesRestartAndIsReconciled()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var regular = await InsertQueuedFileAsync();
        var requested = await InsertQueuedFileAsync();

        // A prioridade era decidida apenas em memória, a partir da lista de
        // solicitações do processo. Depois de um restart essa lista some e a
        // fila restaurada perde a ordem que o usuário havia pedido: um título
        // solicitado voltava a concorrer como trabalho comum.
        Assert.True(await context.SetUploadPriorityAsync(requested, true, CancellationToken.None));

        var persisted = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", requested)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.True(persisted.GetValue("is_priority", false).ToBoolean());

        var untouched = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", regular)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.False(untouched.GetValue("is_priority", false).ToBoolean());

        // Reconciliação após restart: a consulta de pendentes precisa devolver os
        // prioritários primeiro, sem depender de estado em memória.
        var order = new List<ObjectId>();
        await foreach (var doc in context.GetActiveOrPendingUploadsAsync(CancellationToken.None))
        {
            var id = doc.GetValue("_id").AsObjectId;
            if (id == regular || id == requested)
            {
                order.Add(id);
            }
        }

        Assert.Equal(2, order.Count);
        Assert.Equal(requested, order[0]);
    }

    [Fact]
    public async Task ClearingPriority_RestoresRegularOrdering()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();

        Assert.True(await context.SetUploadPriorityAsync(id, true, CancellationToken.None));
        Assert.True(await context.SetUploadPriorityAsync(id, false, CancellationToken.None));

        var document = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);

        // Remover a prioridade apaga o campo em vez de gravar `false`: manter
        // documentos com o campo sempre presente impediria o índice esparso e
        // faria a fila inteira carregar um marcador inútil.
        Assert.False(document.Contains("is_priority"));
    }

    [Fact]
    public async Task CompletedMedia_CannotBePrioritised()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();

        var completedClaim = await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None);
        Assert.NotNull(completedClaim);
        Assert.True(await context.CompleteFileUploadAsync(
            id,
            new BsonDocument { { "status", "completed" }, { "size", 1024L } },
            CancellationToken.None,
            "1",
            completedClaim["lease_id"].AsString));

        // Priorizar mídia já enviada não faz sentido e reabriria trabalho
        // concluído na fila restaurada.
        Assert.False(await context.SetUploadPriorityAsync(id, true, CancellationToken.None));
    }

    [Fact]
    public async Task CatalogRoundTrip_PreservesDocumentsAndCountsAcrossDatabases()
    {
        Assert.SkipUnless(_available, _skipReason);

        // T4.3 exige validar backup/restauração MongoDB com contagens e duração
        // medida. O ciclo Mongo↔Supabase depende de serviço remoto, mas a
        // camada de dados — exportar coleções e reimportá-las num banco limpo,
        // preservando contagem, identidade e campos consultáveis — é exatamente
        // o que decide se uma restauração recupera o catálogo.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        using var source = CreateContext();
        var completedId = await InsertQueuedFileAsync();
        var pendingId = await InsertQueuedFileAsync();

        var completedClaim = await source.ClaimFileForUploadAsync(completedId, 1, 0, CancellationToken.None);
        Assert.NotNull(completedClaim);
        Assert.True(await source.CompleteFileUploadAsync(
            completedId,
            new BsonDocument
            {
                { "status", "completed" },
                { "size", 4096L },
                { "tg_file_id", "file-ref-abc" },
                { "parts", new BsonArray { new BsonDocument { { "part", 0 }, { "size", 4096L } } } }
            },
            CancellationToken.None,
            "1",
            completedClaim["lease_id"].AsString));
        Assert.True(await source.SetUploadPriorityAsync(pendingId, true, CancellationToken.None));

        // "Backup": exporta a coleção inteira como documentos BSON.
        var exported = await Files.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, exported.Count);

        // "Restauração": grava num banco vazio, como faria uma recuperação.
        var restoredDatabaseName = _databaseName + "_restored";
        var restored = _client!.GetDatabase(restoredDatabaseName).GetCollection<BsonDocument>("files");
        try
        {
            await restored.InsertManyAsync(exported, cancellationToken: TestContext.Current.CancellationToken);

            // Contagem preservada.
            Assert.Equal(2, await restored.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken: TestContext.Current.CancellationToken));

            // Consulta crítica: a mídia concluída precisa continuar recuperável
            // com os identificadores Telegram, senão a restauração devolve um
            // catálogo que aponta para lugar nenhum.
            var completedDoc = await restored
                .Find(Builders<BsonDocument>.Filter.Eq("_id", completedId))
                .FirstAsync(TestContext.Current.CancellationToken);
            Assert.Equal("completed", completedDoc["status"].AsString);
            Assert.Equal("file-ref-abc", completedDoc["tg_file_id"].AsString);
            Assert.Equal(4096L, completedDoc["size"].ToInt64());
            Assert.Single(completedDoc["parts"].AsBsonArray);

            // A prioridade persistida sobrevive ao ciclo: sem isso a fila
            // restaurada perderia a preferência do usuário.
            var pendingDoc = await restored
                .Find(Builders<BsonDocument>.Filter.Eq("_id", pendingId))
                .FirstAsync(TestContext.Current.CancellationToken);
            Assert.True(pendingDoc.GetValue("is_priority", false).ToBoolean());

            // Mídia concluída restaurada não pode ser reivindicada de novo.
            using var restoredContext = new NebulaMongoContext(
                _testConnectionString,
                restoredDatabaseName,
                NullLogger<NebulaMongoContext>.Instance);
            Assert.Null(await restoredContext.ClaimFileForUploadAsync(completedId, 9, 0, CancellationToken.None));

            stopwatch.Stop();
            Assert.True(stopwatch.Elapsed > TimeSpan.Zero, "A duração do exercício deve ser medida.");
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromMinutes(2),
                $"Ciclo de restauração demorou demais: {stopwatch.Elapsed}.");
        }
        finally
        {
            // A cancelled test must still remove its isolated restoration database.
            await _client.DropDatabaseAsync(restoredDatabaseName, CancellationToken.None);
        }
    }

    [Fact]
    public async Task RestoredCatalog_KeepsPendingWorkClaimable()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var source = CreateContext();
        var pendingId = await InsertQueuedFileAsync();

        var exported = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", pendingId)).ToListAsync(TestContext.Current.CancellationToken);

        var restoredDatabaseName = _databaseName + "_claimable";
        var restored = _client!.GetDatabase(restoredDatabaseName).GetCollection<BsonDocument>("files");
        try
        {
            await restored.InsertManyAsync(exported, cancellationToken: TestContext.Current.CancellationToken);

            // Trabalho pendente precisa voltar reivindicável: uma restauração que
            // deixasse a fila travada exigiria intervenção manual para retomar.
            using var restoredContext = new NebulaMongoContext(
                _testConnectionString,
                restoredDatabaseName,
                NullLogger<NebulaMongoContext>.Instance);
            var claimed = await restoredContext.ClaimFileForUploadAsync(pendingId, 1, 0, CancellationToken.None);

            Assert.NotNull(claimed);
            Assert.Equal("uploading", claimed!["status"].AsString);
        }
        finally
        {
            // A cancelled test must still remove its isolated restoration database.
            await _client.DropDatabaseAsync(restoredDatabaseName, CancellationToken.None);
        }
    }

    [Fact]
    public async Task RootListing_IncludesCategoryChildrenStoredUnderRaphaelObjectId()
    {
        Assert.SkipUnless(_available, _skipReason);

        var rootId = ObjectId.GenerateNewId();
        var categoryId = ObjectId.GenerateNewId();
        await Files.InsertManyAsync(
            new[]
            {
            new BsonDocument
            {
                ["_id"] = rootId,
                ["name"] = "raphael",
                ["is_directory"] = true,
                ["type"] = "dir",
                ["parent"] = BsonNull.Value
            },
            new BsonDocument
            {
                ["_id"] = categoryId,
                ["name"] = "Animações",
                ["is_directory"] = true,
                ["type"] = "dir",
                ["parent"] = rootId
            }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        using var context = CreateContext();
        var entries = await context.GetChildrenAsync(null, "/", TestContext.Current.CancellationToken);

        Assert.Contains(entries, entry => entry.GetValue("_id").AsObjectId == categoryId);
        Assert.DoesNotContain(entries, entry => entry.GetValue("_id").AsObjectId == rootId);
    }

    [Fact]
    public async Task HasAnyFileDescendant_FollowsObjectIdDirectoryHierarchyToTelegramPayload()
    {
        Assert.SkipUnless(_available, _skipReason);

        var categoryId = ObjectId.GenerateNewId();
        var titleId = ObjectId.GenerateNewId();
        await Files.InsertManyAsync(
            new[]
            {
            new BsonDocument
            {
                ["_id"] = titleId,
                ["name"] = "Title (2026)",
                ["is_directory"] = true,
                ["type"] = "dir",
                ["parent"] = categoryId
            },
            new BsonDocument
            {
                ["_id"] = ObjectId.GenerateNewId(),
                ["name"] = "episode.mkv",
                ["is_directory"] = false,
                ["type"] = "file",
                ["parent"] = titleId,
                ["parts"] = new BsonArray { new BsonDocument("tg_file_id", "test-file") }
            }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        using var context = CreateContext();

        Assert.True(await context.HasAnyFileDescendantAsync(categoryId.ToString(), "/raphael/Series/Title (2026)", TestContext.Current.CancellationToken));
        // The empty virtual path must not reuse the non-empty category's Mongo ID;
        // doing so correctly asks the tree traversal to inspect that category.
        Assert.False(await context.HasAnyFileDescendantAsync(null, "/raphael/Series/Empty", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HasActiveUploads_UsesMongoStatusAndDoesNotRemainActiveAfterCompletion()
    {
        Assert.SkipUnless(_available, _skipReason);

        var id = await InsertQueuedFileAsync();
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("status", "uploading"),
            cancellationToken: TestContext.Current.CancellationToken);

        using var context = CreateContext();
        Assert.True(await context.HasActiveUploadsAsync(TestContext.Current.CancellationToken));

        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("status", "completed"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(await context.HasActiveUploadsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HasActiveUploads_IgnoresInterruptedItemsFlaggedForRecovery()
    {
        Assert.SkipUnless(_available, _skipReason);

        var id = await InsertQueuedFileAsync();
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update
                .Set("status", "uploading")
                .Set("recovery_required", true),
            cancellationToken: TestContext.Current.CancellationToken);

        using var context = CreateContext();
        Assert.False(await context.HasActiveUploadsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AutomaticSupabaseBackup_WaitsWhileTelegramUploadIsActive()
    {
        Assert.SkipUnless(_available, _skipReason);

        var id = await InsertQueuedFileAsync();
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("status", "uploading"),
            cancellationToken: TestContext.Current.CancellationToken);

        using var context = CreateContext();
        await using var activeUpload = await context.AcquireUploadAdmissionAsync(TestContext.Current.CancellationToken);
        using var handler = new UnexpectedHttpHandler();
        using var service = new NebulaSupabaseSyncService(
            context,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            null!,
            handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PerformAutomaticBackupWhenIdleAsync(
            "https://supabase.invalid",
            "sb_secret_test",
            progressAction: null,
            cancellation.Token));
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task BackupAdmission_DrainsCurrentUploadsAndBlocksNewOnes()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var currentUpload = await context.AcquireUploadAdmissionAsync(timeout.Token);

        var backupTask = context.AcquireExclusiveSyncAdmissionAsync(null, timeout.Token);
        try
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                var state = await Admission.Find(Builders<BsonDocument>.Filter.Eq("_id", NebulaMongoContext.AdmissionGateId))
                    .FirstOrDefaultAsync(timeout.Token);
                if (state is not null && state.Contains("backup_owner"))
                {
                    break;
                }

                await Task.Delay(25, timeout.Token);
            }

            var backupState = await Admission.Find(Builders<BsonDocument>.Filter.Eq("_id", NebulaMongoContext.AdmissionGateId))
                .FirstOrDefaultAsync(timeout.Token);
            Assert.NotNull(backupState);
            Assert.True(backupState.Contains("backup_owner"), "Backup deve reservar novas admissões antes de drenar uploads ativos.");

            var waitingUpload = context.AcquireUploadAdmissionAsync(timeout.Token);
            await Task.Delay(100, timeout.Token);
            Assert.False(backupTask.IsCompleted, "Backup não pode iniciar antes do upload existente terminar.");
            Assert.False(waitingUpload.IsCompleted, "Novos uploads devem aguardar enquanto o backup possui a reserva.");

            await currentUpload.DisposeAsync();
            await using var backup = await backupTask;
            Assert.False(waitingUpload.IsCompleted, "A reserva continua bloqueando uploads durante o backup.");

            await backup.DisposeAsync();
            await using var nextUpload = await waitingUpload;
        }
        finally
        {
            if (!backupTask.IsCompleted)
            {
                timeout.Cancel();
                try
                {
                    await backupTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }

    [Fact]
    public async Task BackupAdmission_RemovesExpiredUploadLeases()
    {
        Assert.SkipUnless(_available, _skipReason);

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await Admission.InsertOneAsync(
            new BsonDocument
            {
            { "_id", NebulaMongoContext.AdmissionGateId },
            { "backup_expires_at", now - 1 },
            {
                "active_upload_leases",
                new BsonArray
                {
                    new BsonDocument { { "lease_id", "expired" }, { "expires_at", now - 1 } }
                }
            }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        using var context = CreateContext();
        await using var backup = await context.AcquireExclusiveSyncAdmissionAsync(null, TestContext.Current.CancellationToken);

        var state = await Admission.Find(Builders<BsonDocument>.Filter.Eq("_id", NebulaMongoContext.AdmissionGateId))
            .FirstAsync(TestContext.Current.CancellationToken);
        Assert.Empty(state["active_upload_leases"].AsBsonArray);
    }

    [Fact]
    [Trait("Category", "RequiresMongoFailPoint")]
    public async Task AcceptedTelegramPart_IsRecoveredAfterMongoProgressWriteFails()
    {
        Assert.SkipUnless(_available, _skipReason);

        var admin = _client!.GetDatabase("admin");
        try
        {
            await admin.RunCommandAsync<BsonDocument>(
                new BsonDocument
                {
                    { "configureFailPoint", "failCommand" },
                    { "mode", "off" }
                },
                cancellationToken: TestContext.Current.CancellationToken);
        }
        catch (MongoCommandException exception) when (exception.Code == 59)
        {
            Assert.Skip("Mongo opt-in precisa habilitar enableTestCommands para injetar a falha de checkpoint.");
        }

        var sourceDirectory = Path.Combine(Path.GetTempPath(), "nebula-upload-recovery-" + Guid.NewGuid().ToString("N"));
        var checkpointDirectory = Path.Combine(sourceDirectory, "checkpoints");
        var sessionsDirectory = Path.Combine(sourceDirectory, "sessions");
        Directory.CreateDirectory(sourceDirectory);
        var sourcePath = Path.Combine(sourceDirectory, "recovery.mkv");
        await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4, 5], TestContext.Current.CancellationToken);

        var handler = new TelegramAckThenFailMongoHandler(_client);
        var httpClient = new HttpClient(handler);
        var telegramPool = new NebulaTelegramPool(
            1,
            "test-api-hash",
            ["123456:test-token"],
            -1001234567890,
            sessionsDirectory,
            NullLogger<NebulaTelegramPool>.Instance,
            httpClient);
        var clientsField = typeof(NebulaTelegramPool).GetField(
            "_clients",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(clientsField);
        var authenticatedClients = Assert.IsType<ConcurrentDictionary<int, Client>>(clientsField.GetValue(telegramPool));
        authenticatedClients.TryAdd(0, (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client)));

        try
        {
            using var firstEngine = new NebulaUploadEngine(
                CreateContext(),
                telegramPool,
                uploadConcurrency: 1,
                chunkSizeMb: 16,
                deleteSourceAfterUpload: false,
                NullLogger<NebulaUploadEngine>.Instance,
                uploadCheckpointDirectory: checkpointDirectory);

            await Assert.ThrowsAnyAsync<MongoException>(
                () => firstEngine.ProcessFileUploadAsync(sourcePath, "recovery.mkv", cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal(1, handler.RequestCount);
            var failedDocument = await Files.Find(Builders<BsonDocument>.Filter.Eq("local_path", sourcePath))
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Empty(failedDocument["parts"].AsBsonArray);
            Assert.Single(Directory.GetFiles(checkpointDirectory, "*.json"));

            await Files.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", failedDocument["_id"].AsObjectId),
                Builders<BsonDocument>.Update
                    .Set("status", "queued")
                    .Set("retry_after", 0L)
                    .Unset("worker_id")
                    .Unset("lease_id")
                    .Unset("lease_until")
                    .Unset("heartbeat_at")
                    .Unset("started_at"),
                cancellationToken: TestContext.Current.CancellationToken);

            using var resumedEngine = new NebulaUploadEngine(
                CreateContext(),
                telegramPool,
                uploadConcurrency: 1,
                chunkSizeMb: 16,
                deleteSourceAfterUpload: false,
                NullLogger<NebulaUploadEngine>.Instance,
                uploadCheckpointDirectory: checkpointDirectory);

            Assert.True(await resumedEngine.ProcessFileUploadAsync(
                sourcePath,
                "recovery.mkv",
                cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal(1, handler.RequestCount);
            var completedDocument = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", failedDocument["_id"].AsObjectId))
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("completed", completedDocument["status"].AsString);
            Assert.Single(completedDocument["parts"].AsBsonArray);
            Assert.Equal("accepted-file-id", completedDocument["parts"].AsBsonArray[0].AsBsonDocument["tg_file_id"].AsString);
            Assert.Empty(Directory.GetFiles(checkpointDirectory, "*.json"));
        }
        finally
        {
            authenticatedClients.Clear();
            await telegramPool.DisposeAsync();
            Directory.Delete(sourceDirectory, recursive: true);
            await admin.RunCommandAsync<BsonDocument>(
                new BsonDocument
                {
                    { "configureFailPoint", "failCommand" },
                    { "mode", "off" }
                },
                cancellationToken: CancellationToken.None);
        }
    }

    [Fact]
    public async Task LostTelegramResponse_IsReconciledFromHistoryBeforeRestartRetry()
    {
        Assert.SkipUnless(_available, _skipReason);

        var sourceDirectory = Path.Combine(Path.GetTempPath(), "nebula-upload-ambiguous-" + Guid.NewGuid().ToString("N"));
        var checkpointDirectory = Path.Combine(sourceDirectory, "checkpoints");
        var sessionsDirectory = Path.Combine(sourceDirectory, "sessions");
        Directory.CreateDirectory(sourceDirectory);
        var sourcePath = Path.Combine(sourceDirectory, "ambiguous.mkv");
        await File.WriteAllBytesAsync(sourcePath, [1, 2, 3, 4, 5], TestContext.Current.CancellationToken);

        var handler = new TelegramLostResponseHandler();
        var allowHistoryRecovery = 0;
        var historyLookupCount = 0;
        var uploadLogger = new CapturingLogger<NebulaUploadEngine>();
        var httpClient = new HttpClient(handler);
        var telegramPool = new NebulaTelegramPool(
            1,
            "test-api-hash",
            ["123456:test-token", "123456:test-token-2"],
            -1001234567890,
            sessionsDirectory,
            NullLogger<NebulaTelegramPool>.Instance,
            httpClient,
            (botIndex, fileUuid, partNumber, totalParts, _) =>
            {
                Interlocked.Increment(ref historyLookupCount);
                return Task.FromResult(Volatile.Read(ref allowHistoryRecovery) == 0
                    ? NebulaTelegramPartLookupResult.Inconclusive
                    : NebulaTelegramPartLookupResult.Found(
                        new NebulaTelegramUploadResult(botIndex, 456, -1001234567890, "mtproto:456")));
            });
        var clientsField = typeof(NebulaTelegramPool).GetField(
            "_clients",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(clientsField);
        var authenticatedClients = Assert.IsType<ConcurrentDictionary<int, Client>>(clientsField.GetValue(telegramPool));
        authenticatedClients.TryAdd(0, (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client)));
        authenticatedClients.TryAdd(1, (Client)RuntimeHelpers.GetUninitializedObject(typeof(Client)));
        var cooldownsField = typeof(NebulaTelegramPool).GetField(
            "_uploadBotCooldowns",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(cooldownsField);
        var cooldowns = Assert.IsType<ConcurrentDictionary<int, long>>(cooldownsField.GetValue(telegramPool));
        // The engine asks for bot 0, but the pool must route around its cooldown
        // and use bot 1. The ambiguous result must still be associated with the
        // actual sender, not the bot originally requested by the worker.
        cooldowns[0] = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds();

        try
        {
            using var firstEngine = new NebulaUploadEngine(
                CreateContext(),
                telegramPool,
                uploadConcurrency: 1,
                chunkSizeMb: 16,
                deleteSourceAfterUpload: false,
                uploadLogger,
                uploadCheckpointDirectory: checkpointDirectory);

            Assert.False(await firstEngine.ProcessFileUploadAsync(
                sourcePath,
                "ambiguous.mkv",
                cancellationToken: TestContext.Current.CancellationToken));
            // Even with a second bot ready, an unresolved response must release
            // the worker for scheduled automatic retry instead of sending a copy.
            Assert.Equal(1, handler.RequestCount);
            Assert.Equal(8, historyLookupCount);
            Assert.Single(Directory.GetFiles(checkpointDirectory, "*.json"));

            var failedDocument = await Files.Find(Builders<BsonDocument>.Filter.Eq("local_path", sourcePath))
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Empty(failedDocument["parts"].AsBsonArray);
            Assert.True(
                string.Equals("failed", failedDocument["status"].AsString, StringComparison.Ordinal),
                failedDocument.ToJson() + Environment.NewLine + string.Join(Environment.NewLine, uploadLogger.Messages));
            Assert.Equal(1, failedDocument["retry_count"].ToInt32());
            Assert.True(failedDocument["retry_after"].ToInt64() > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            await Files.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", failedDocument["_id"].AsObjectId),
                Builders<BsonDocument>.Update
                    .Set("status", "queued")
                    .Set("retry_after", 0L)
                    .Unset("worker_id")
                    .Unset("lease_id")
                    .Unset("lease_until")
                    .Unset("heartbeat_at")
                    .Unset("started_at"),
                cancellationToken: TestContext.Current.CancellationToken);

            Volatile.Write(ref allowHistoryRecovery, 1);
            using var resumedEngine = new NebulaUploadEngine(
                CreateContext(),
                telegramPool,
                uploadConcurrency: 1,
                chunkSizeMb: 16,
                deleteSourceAfterUpload: false,
                NullLogger<NebulaUploadEngine>.Instance,
                uploadCheckpointDirectory: checkpointDirectory);

            Assert.True(await resumedEngine.ProcessFileUploadAsync(
                sourcePath,
                "ambiguous.mkv",
                cancellationToken: TestContext.Current.CancellationToken));

            Assert.Equal(1, handler.RequestCount);
            var completedDocument = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", failedDocument["_id"].AsObjectId))
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("completed", completedDocument["status"].AsString);
            Assert.Equal("mtproto:456", completedDocument["parts"].AsBsonArray[0].AsBsonDocument["tg_file_id"].AsString);
            Assert.Equal(9, historyLookupCount);
            Assert.Empty(Directory.GetFiles(checkpointDirectory, "*.json"));
        }
        finally
        {
            authenticatedClients.Clear();
            await telegramPool.DisposeAsync();
            Directory.Delete(sourceDirectory, recursive: true);
        }
    }

    private sealed class TelegramAckThenFailMongoHandler : HttpMessageHandler
    {
        private const string TelegramAcceptedResponse = "{\"ok\":true,\"result\":{\"message_id\":123,\"chat\":{\"id\":-1001234567890},\"document\":{\"file_id\":\"accepted-file-id\"}}}";
        private readonly MongoClient _client;
        private int _requestCount;

        public TelegramAckThenFailMongoHandler(MongoClient client)
        {
            _client = client;
        }

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _requestCount) == 1)
            {
                await _client.GetDatabase("admin").RunCommandAsync<BsonDocument>(
                    new BsonDocument
                    {
                        { "configureFailPoint", "failCommand" },
                        { "mode", new BsonDocument("times", 2) },
                        {
                            "data",
                            new BsonDocument
                            {
                                { "failCommands", new BsonArray { "update" } },
                                { "errorCode", 11600 }
                            }
                        }
                    },
                    cancellationToken: cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(TelegramAcceptedResponse, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class TelegramLostResponseHandler : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            throw new HttpRequestException("Simulated connection loss after Telegram accepted the document.");
        }
    }

    public void Dispose()
    {
        if (_available && _client is not null)
        {
            _client.DropDatabase(_databaseName);
        }
    }

    private sealed class UnexpectedHttpHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            throw new InvalidOperationException("The automatic backup must wait for active uploads before sending HTTP requests.");
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Enqueue(formatter(state, exception));
        }
    }
}
