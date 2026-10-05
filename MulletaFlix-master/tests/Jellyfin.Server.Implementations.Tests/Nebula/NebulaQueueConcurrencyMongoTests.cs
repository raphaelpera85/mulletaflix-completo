using System;
using System.Collections.Generic;
using System.Linq;
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

    private NebulaMongoContext CreateContext()
        => new(_testConnectionString, _databaseName, NullLogger<NebulaMongoContext>.Instance);

    private IMongoCollection<BsonDocument> Files
        => _client!.GetDatabase(_databaseName).GetCollection<BsonDocument>("files");

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

        var document = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();
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

        var ownerParts = new BsonArray { new BsonDocument { { "part", 0 }, { "size", 512L } } };
        var ownerWrote = await context.UpdateUploadProgressAsync(id, ownerParts, 512, 0, "1", CancellationToken.None);
        Assert.True(ownerWrote, "O dono do lease deve conseguir gravar progresso.");

        // Worker 2 nunca reivindicou este documento.
        var intruderParts = new BsonArray { new BsonDocument { { "part", 99 }, { "size", 1L } } };
        var intruderWrote = await context.UpdateUploadProgressAsync(id, intruderParts, 1, 0, "2", CancellationToken.None);
        Assert.False(intruderWrote, "Worker sem posse não pode gravar progresso.");

        // E as partes do dono continuam intactas.
        var document = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();
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
        Assert.NotNull(await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None));
        Assert.True(await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 512L } } },
            512,
            0,
            "1",
            CancellationToken.None));

        // O worker travou e a recuperação devolveu o item à fila.
        await context.RequeueInterruptedUploadsAsync(CancellationToken.None);
        var requeued = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();

        // Só é requeuado se o lease tiver expirado; para o teste, força a expiração.
        if (requeued["status"].AsString != "queued")
        {
            await Files.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.Set("lease_until", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 10));
            await context.RequeueInterruptedUploadsAsync(CancellationToken.None);
            requeued = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();
        }

        Assert.Equal("queued", requeued["status"].AsString);
        Assert.False(requeued.Contains("worker_id"), "O requeue deve remover a posse.");
        Assert.False(requeued.Contains("lease_id"), "O requeue deve limpar o lease.");

        // Worker 2 assume legitimamente.
        Assert.NotNull(await context.ClaimFileForUploadAsync(id, 2, 0, CancellationToken.None));
        Assert.True(await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 900L } } },
            900,
            0,
            "2",
            CancellationToken.None));

        // O worker 1 volta do limbo. Esta é a corrida que corrompia os dados:
        // antes do fencing, "sem dono" o qualificava e ele sobrescrevia o
        // progresso do worker 2.
        var zombieWrote = await context.UpdateUploadProgressAsync(
            id,
            new BsonArray { new BsonDocument { { "part", 0 }, { "size", 512L } } },
            512,
            0,
            "1",
            CancellationToken.None);

        Assert.False(zombieWrote, "Worker zumbi não pode readotar documento já reivindicado.");

        var final = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();
        Assert.Equal("2", final["worker_id"].AsString);
        Assert.Equal(900L, final["uploaded_bytes"].ToInt64());
    }

    [Fact]
    public async Task CompletedUpload_IsNotReclaimedByANewClaim()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();

        Assert.NotNull(await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None));
        var completed = await context.CompleteFileUploadAsync(
            id,
            new BsonDocument
            {
                { "status", "completed" },
                { "size", 1024L },
                { "parts", new BsonArray { new BsonDocument { { "part", 0 }, { "size", 1024L } } } }
            },
            CancellationToken.None,
            "1");
        Assert.True(completed);

        // Mídia concluída nunca volta para a fila: é o requisito de idempotência
        // de T2.2 (não duplicar arquivos já enviados).
        var reclaim = await context.ClaimFileForUploadAsync(id, 2, 0, CancellationToken.None);
        Assert.Null(reclaim);

        var document = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();
        Assert.Equal("completed", document["status"].AsString);
        Assert.False(document.Contains("lease_id"), "A conclusão deve limpar o lease.");
    }

    [Fact]
    public async Task FailedUpload_HonoursRetryAfterBeforeBeingClaimedAgain()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var context = CreateContext();
        var id = await InsertQueuedFileAsync();

        Assert.NotNull(await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None));
        Assert.True(await context.MarkUploadFailedAsync(
            id,
            "falha transitória",
            NebulaUploadFailureStages.TelegramTransfer,
            CancellationToken.None,
            "1"));

        var failed = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();
        Assert.Equal("failed", failed["status"].AsString);
        Assert.True(failed.Contains("retry_after"), "Falha transitória deve agendar retry.");

        // Enquanto o backoff não vence, nenhum worker pode reivindicar.
        Assert.Null(await context.ClaimFileForUploadAsync(id, 2, 0, CancellationToken.None));

        // Vencido o backoff, o item volta a ser elegível.
        await Files.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Update.Set("retry_after", DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1));
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

        var persisted = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", requested)).FirstAsync();
        Assert.True(persisted.GetValue("is_priority", false).ToBoolean());

        var untouched = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", regular)).FirstAsync();
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

        var document = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstAsync();

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

        Assert.NotNull(await context.ClaimFileForUploadAsync(id, 1, 0, CancellationToken.None));
        Assert.True(await context.CompleteFileUploadAsync(
            id,
            new BsonDocument { { "status", "completed" }, { "size", 1024L } },
            CancellationToken.None,
            "1"));

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

        Assert.NotNull(await source.ClaimFileForUploadAsync(completedId, 1, 0, CancellationToken.None));
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
            "1"));
        Assert.True(await source.SetUploadPriorityAsync(pendingId, true, CancellationToken.None));

        // "Backup": exporta a coleção inteira como documentos BSON.
        var exported = await Files.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync();
        Assert.Equal(2, exported.Count);

        // "Restauração": grava num banco vazio, como faria uma recuperação.
        var restoredDatabaseName = _databaseName + "_restored";
        var restored = _client!.GetDatabase(restoredDatabaseName).GetCollection<BsonDocument>("files");
        try
        {
            await restored.InsertManyAsync(exported);

            // Contagem preservada.
            Assert.Equal(2, await restored.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty));

            // Consulta crítica: a mídia concluída precisa continuar recuperável
            // com os identificadores Telegram, senão a restauração devolve um
            // catálogo que aponta para lugar nenhum.
            var completedDoc = await restored
                .Find(Builders<BsonDocument>.Filter.Eq("_id", completedId))
                .FirstAsync();
            Assert.Equal("completed", completedDoc["status"].AsString);
            Assert.Equal("file-ref-abc", completedDoc["tg_file_id"].AsString);
            Assert.Equal(4096L, completedDoc["size"].ToInt64());
            Assert.Single(completedDoc["parts"].AsBsonArray);

            // A prioridade persistida sobrevive ao ciclo: sem isso a fila
            // restaurada perderia a preferência do usuário.
            var pendingDoc = await restored
                .Find(Builders<BsonDocument>.Filter.Eq("_id", pendingId))
                .FirstAsync();
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
            await _client.DropDatabaseAsync(restoredDatabaseName);
        }
    }

    [Fact]
    public async Task RestoredCatalog_KeepsPendingWorkClaimable()
    {
        Assert.SkipUnless(_available, _skipReason);

        using var source = CreateContext();
        var pendingId = await InsertQueuedFileAsync();

        var exported = await Files.Find(Builders<BsonDocument>.Filter.Eq("_id", pendingId)).ToListAsync();

        var restoredDatabaseName = _databaseName + "_claimable";
        var restored = _client!.GetDatabase(restoredDatabaseName).GetCollection<BsonDocument>("files");
        try
        {
            await restored.InsertManyAsync(exported);

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
            await _client.DropDatabaseAsync(restoredDatabaseName);
        }
    }

    public void Dispose()
    {
        if (_available && _client is not null)
        {
            _client.DropDatabase(_databaseName);
        }
    }
}
