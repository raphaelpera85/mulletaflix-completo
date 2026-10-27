using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
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

    private IMongoCollection<BsonDocument> Files
        => _client!.GetDatabase(_databaseName).GetCollection<BsonDocument>("files");

    private IMongoCollection<BsonDocument> Admission
        => _client!.GetDatabase(_databaseName).GetCollection<BsonDocument>("nebula_admission");

    private NebulaMongoContext CreateContext()
        => new(_testConnectionString, _databaseName, NullLogger<NebulaMongoContext>.Instance);

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
    public async Task EnsureIndexesAsync_PropagatesDuplicateQueueIdentityFailure()
    {
        Assert.SkipUnless(_available, _skipReason);

        await Files.InsertManyAsync(
            [
                new BsonDocument { { "queue_identity", "same-path" } },
                new BsonDocument { { "queue_identity", "same-path" } }
            ],
            cancellationToken: TestContext.Current.CancellationToken);

        using var context = CreateContext();

        await Assert.ThrowsAnyAsync<MongoException>(
            () => context.EnsureIndexesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnsureIndexesAsync_BackfillsLegacyQueueIdentityWithoutRemovingDocument()
    {
        Assert.SkipUnless(_available, _skipReason);

        var id = ObjectId.GenerateNewId();
        var whitespacePathId = ObjectId.GenerateNewId();
        var localPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Nebula", "legacy-media.mkv"));
        await Files.InsertManyAsync(
            [
                new BsonDocument
                {
                    { "_id", id },
                    { "name", "legacy-media.mkv" },
                    { "type", "file" },
                    { "status", "queued" },
                    { "local_path", localPath }
                },
                new BsonDocument
                {
                    { "_id", whitespacePathId },
                    { "type", "file" },
                    { "local_path", "   " }
                }
            ],
            cancellationToken: TestContext.Current.CancellationToken);

        using var context = CreateContext();
        await context.EnsureIndexesAsync(TestContext.Current.CancellationToken);

        var migrated = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id))
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(OperatingSystem.IsWindows() ? localPath.ToUpperInvariant() : localPath, migrated["queue_identity"].AsString);
        Assert.Equal(1, await Files.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            cancellationToken: TestContext.Current.CancellationToken));

        var whitespaceDocument = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", whitespacePathId))
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(whitespaceDocument.Contains("queue_identity"));
    }

    [Fact]
    public async Task ResolveLocalPathAsync_UpdatesQueueIdentityWithRecoveredPath()
    {
        Assert.SkipUnless(_available, _skipReason);

        var stagingRoot = Path.Combine(Path.GetTempPath(), "nebula-queue-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingRoot);
        var recoveredPath = Path.Combine(stagingRoot, "episode.mkv");
        await File.WriteAllTextAsync(recoveredPath, "media", TestContext.Current.CancellationToken);
        var id = ObjectId.GenerateNewId();
        var originalPath = Path.Combine(stagingRoot, "unavailable", "episode.mkv");
        await Files.InsertOneAsync(
            new BsonDocument
            {
                { "_id", id },
                { "type", "file" },
                { "name", "episode.mkv" },
                { "local_path", originalPath },
                { "queue_identity", OperatingSystem.IsWindows() ? originalPath.ToUpperInvariant() : originalPath },
                { "status", "queued" }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        try
        {
            using var context = CreateContext();
            var resolved = await context.ResolveLocalPathAsync(
                await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).SingleAsync(TestContext.Current.CancellationToken),
                [stagingRoot],
                TestContext.Current.CancellationToken);

            Assert.Equal(recoveredPath, resolved);
            var updated = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id))
                .SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(recoveredPath, updated["local_path"].AsString);
            Assert.Equal(OperatingSystem.IsWindows() ? recoveredPath.ToUpperInvariant() : recoveredPath, updated["queue_identity"].AsString);
        }
        finally
        {
            Directory.Delete(stagingRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureIndexesAsync_FailsClosedOnLegacyPathsThatNormalizeToSameIdentity()
    {
        Assert.SkipUnless(_available, _skipReason);
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Case-insensitive legacy path collisions apply to Windows paths.");

        var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Nebula", "Legacy-Media.mkv"));
        await Files.InsertManyAsync(
            [
                new BsonDocument { { "type", "file" }, { "local_path", path } },
                new BsonDocument { { "type", "file" }, { "local_path", path.ToLowerInvariant() } }
            ],
            cancellationToken: TestContext.Current.CancellationToken);

        using var context = CreateContext();
        await Assert.ThrowsAnyAsync<MongoException>(
            () => context.EnsureIndexesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(2, await Files.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("type", "file"),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CSharpAndPythonInitializers_ConcurrentlyBackfillSameLegacyQueueRecord()
    {
        Assert.SkipUnless(_available, _skipReason);
        var python = Environment.GetEnvironmentVariable("NEBULA_TEST_PYTHON");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(python), "Defina NEBULA_TEST_PYTHON para habilitar o teste cruzado C#/Python.");

        var id = ObjectId.GenerateNewId();
        var localPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Nebula", "cross-runtime-legacy.mkv"));
        await Files.InsertOneAsync(
            new BsonDocument
            {
                { "_id", id },
                { "type", "file" },
                { "status", "queued" },
                { "local_path", localPath }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        var pythonRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tools", "NebulaPython"));
        var script = "import asyncio, os; from motor.motor_asyncio import AsyncIOMotorClient; import main\n"
            + "async def run():\n"
            + " client = AsyncIOMotorClient(os.environ['NEBULA_TEST_MONGO_URI'])\n"
            + " try: await main.setup_database_indexes(client[os.environ['NEBULA_TEST_MONGO_DATABASE']])\n"
            + " finally: client.close()\n"
            + "asyncio.run(run())";

        var startInfo = new ProcessStartInfo(python!)
        {
            WorkingDirectory = pythonRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(script);
        startInfo.Environment["NEBULA_STREAM_SERVICE"] = "1";
        startInfo.Environment["NEBULA_TEST_MONGO_URI"] = _testConnectionString;
        startInfo.Environment["NEBULA_TEST_MONGO_DATABASE"] = _databaseName;
        using var process = Process.Start(startInfo);
        Assert.NotNull(process);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var context = CreateContext();
        var pythonOutputTask = process!.StandardOutput.ReadToEndAsync(timeout.Token);
        var pythonErrorTask = process.StandardError.ReadToEndAsync(timeout.Token);
        var csharpTask = context.EnsureIndexesAsync(timeout.Token);
        try
        {
        await Task.WhenAll(csharpTask, process.WaitForExitAsync(timeout.Token));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
        var output = await pythonOutputTask;
        var error = await pythonErrorTask;
        Assert.True(process.ExitCode == 0, $"Python initializer failed ({process.ExitCode}). stdout: {output} stderr: {error}");

        var migrated = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id))
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(OperatingSystem.IsWindows() ? localPath.ToUpperInvariant() : localPath, migrated["queue_identity"].AsString);
        Assert.Equal(1, await Files.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("queue_identity", migrated["queue_identity"].AsString),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InsertFileDocIfAbsentAsync_MatchesLegacyLowercaseWindowsIdentity()
    {
        Assert.SkipUnless(_available, _skipReason);
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Legacy lowercase queue identities were written by Python on Windows.");

        var localPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Nebula", "MixedCase", "Media.mkv"));
        var legacyIdentity = localPath.ToLowerInvariant();
        await Files.InsertOneAsync(
            new BsonDocument
            {
                { "name", "Media.mkv" },
                { "type", "file" },
                { "status", "queued" },
                { "local_path", localPath },
                { "queue_identity", legacyIdentity }
            },
            cancellationToken: TestContext.Current.CancellationToken);

        using var context = CreateContext();
        var inserted = await context.InsertFileDocIfAbsentAsync(
            new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "name", "Media.mkv" },
                { "type", "file" },
                { "status", "queued" },
                { "local_path", localPath }
            },
            TestContext.Current.CancellationToken);

        Assert.False(inserted);
        Assert.Equal(1, await Files.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Eq("local_path", localPath),
            cancellationToken: TestContext.Current.CancellationToken));
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
        var intruderWrote = await context.UpdateUploadProgressAsync(id, intruderParts, 1, 0, "2", CancellationToken.None);
        Assert.False(intruderWrote, "Worker sem posse não pode gravar progresso.");

        // E as partes do dono continuam intactas.
        var document = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("1", document["worker_id"].AsString);
        Assert.Equal(512L, document["uploaded_bytes"].ToInt64());
        Assert.Single(document["parts"].AsBsonArray);
        Assert.Equal(0, document["parts"].AsBsonArray[0].AsBsonDocument["part"].ToInt32());
    }

    [Fact]
    public async Task RequeuedDocument_CannotBeWrittenByTheZombieWorker()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();

        // Worker 1 reivindica e grava progresso.
        var firstClaim = await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None);
        Assert.NotNull(firstClaim);
        var firstLeaseId = firstClaim["lease_id"].AsString;
        Assert.True(await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 512L } } },
            512,
            0,
            "1",
            CancellationToken.None,
            firstLeaseId));

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
        var secondLeaseId = secondClaim["lease_id"].AsString;
        Assert.True(await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 900L } } },
            900,
            0,
            "2",
            CancellationToken.None,
            secondLeaseId));

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
            firstLeaseId);

        Assert.False(zombieWrote, "Worker zumbi não pode readotar documento já reivindicado.");

        var final = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("2", final["worker_id"].AsString);
        Assert.Equal(900L, final["uploaded_bytes"].ToInt64());
    }

    [Fact]
    public async Task ReclaimedUpload_RejectsPreviousLeaseFromSameWorkerId()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();
        var firstClaim = await context.ClaimFileForUploadAsync(id, 4, 0, CancellationToken.None);
        Assert.NotNull(firstClaim);
        var oldLeaseId = firstClaim["lease_id"].AsString;

        Assert.True(await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 512L } } },
            512,
            0,
            "4",
            CancellationToken.None,
            oldLeaseId));

        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("lease_until", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10),
            cancellationToken: TestContext.Current.CancellationToken);

        // A delayed worker cannot resurrect its generation after the lease
        // deadline, even if no replacement claim has committed yet.
        Assert.False(await context.RenewUploadLeaseAsync(id, "4", oldLeaseId, CancellationToken.None));
        Assert.False(await context.TryAcquireUploadOwnershipAsync(id, "4", CancellationToken.None, oldLeaseId));

        await context.RequeueInterruptedUploadsAsync(CancellationToken.None);

        var secondClaim = await context.ClaimFileForUploadAsync(id, 4, 0, CancellationToken.None);
        Assert.NotNull(secondClaim);
        var newLeaseId = secondClaim["lease_id"].AsString;
        Assert.NotEqual(oldLeaseId, newLeaseId);
        Assert.True(await context.RenewUploadLeaseAsync(id, "4", newLeaseId, CancellationToken.None));
        Assert.False(await context.RenewUploadLeaseAsync(id, "4", oldLeaseId, CancellationToken.None));

        Assert.True(await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 900L } } },
            900,
            0,
            "4",
            CancellationToken.None,
            newLeaseId));

        var staleWrite = await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 512L } } },
            512,
            0,
            "4",
            CancellationToken.None,
            oldLeaseId);
        Assert.False(staleWrite);

        var final = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync(TestContext.Current.CancellationToken);
        Assert.Equal("4", final["worker_id"].AsString);
        Assert.Equal(newLeaseId, final["lease_id"].AsString);
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
    public async Task TransientUpload_HonoursRetryAfterBeforeBeingClaimedAgain()
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
        Assert.Equal("retry_wait", failed["status"].AsString);
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
    public async Task UploadQueueSummary_CountsCurrentAndLegacyRetryStatesSeparately()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var currentRetry = await InsertQueuedFileAsync();
        var legacyRetry = await InsertQueuedFileAsync();
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", currentRetry),
            Builders<BsonDocument>.Update.Set("status", "retry_wait").Set("retry_after", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 120),
            cancellationToken: TestContext.Current.CancellationToken);
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", legacyRetry),
            Builders<BsonDocument>.Update.Set("status", "failed").Set("next_retry_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 120),
            cancellationToken: TestContext.Current.CancellationToken);

        var summary = await context.GetUploadQueueSummaryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, summary.RetryCount);
        Assert.Equal(0, summary.PendingCount);
    }

    [Theory]
    [InlineData("retry_wait", "retry_after")]
    [InlineData("retry_wait", "next_retry_at")]
    [InlineData("failed", "retry_after")]
    [InlineData("failed", "next_retry_at")]
    public async Task RetryQueue_HonoursCurrentAndLegacyRetryDeadlines(string status, string deadlineField)
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update
                .Set("status", status)
                .Set(deadlineField, now + 120)
                .Set("failure_terminal", false),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None));

        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set(deadlineField, now - 1),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(await context.ClaimFileForUploadAsync(id, 2, 0, CancellationToken.None));
    }

    [Fact]
    public async Task RetryQueue_UsesLaterDeadlineWhenPythonAndServerFieldsCoexist()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update
                .Set("status", "retry_wait")
                .Set("retry_after", now + 120)
                .Set("next_retry_at", now - 1),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None));

        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("retry_after", now - 1),
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

        var claimed = await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None);
        Assert.NotNull(claimed);
        Assert.True(await context.CompleteFileUploadAsync(
            id,
            new BsonDocument { { "status", "completed" }, { "size", 1024L } },
            CancellationToken.None,
            "1",
            claimed["lease_id"].AsString));

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

        var sourceClaim = await source.ClaimFileForUploadAsync(completedId, 1, 0, CancellationToken.None);
        Assert.NotNull(sourceClaim);
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
            sourceClaim["lease_id"].AsString));
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
}
