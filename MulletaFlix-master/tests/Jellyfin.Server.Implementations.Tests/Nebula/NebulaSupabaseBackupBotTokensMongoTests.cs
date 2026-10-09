using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using MulletaFlix.Database.Implementations.Contexts;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Verifies that backup persists Telegram bot tokens (ftp.bot_tokens) into Supabase's
/// nebula_bot_tokens table, and that a subsequent restore rehydrates them back into
/// MongoDB. Regression coverage for the bug where backup silently skipped bot tokens,
/// leaving the remote table empty and making restore always report zero tokens.
/// </summary>
[Trait("Category", "RequiresMongo")]
public sealed class NebulaSupabaseBackupBotTokensMongoTests : IDisposable
{
    private readonly string _databaseName = "nebula_t43_backup_tokens_" + Guid.NewGuid().ToString("N");
    private readonly string _testConnectionString;
    private readonly MongoClient? _mongoClient;
    private readonly bool _mongoAvailable;
    private readonly string _skipReason;

    public NebulaSupabaseBackupBotTokensMongoTests()
    {
        if (!NebulaMongoTestConnection.TryGet(out var testConnectionString, out var skipReason))
        {
            _testConnectionString = string.Empty;
            _skipReason = skipReason;
            _mongoAvailable = false;
            return;
        }

        _testConnectionString = testConnectionString;
        _skipReason = string.Empty;
        try
        {
            var settings = MongoClientSettings.FromConnectionString(_testConnectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
            _mongoClient = new MongoClient(settings);
            _mongoClient.GetDatabase("admin").RunCommand<BsonDocument>(new BsonDocument("ping", 1));
            _mongoAvailable = true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Falha ao validar a conexão Mongo de teste configurada em 127.0.0.1:27099.",
                ex);
        }
    }

    [Fact]
    public async Task Backup_PersistsBotTokensIntoSupabaseAndRestoreRehydratesThemIntoMongo()
    {
        Assert.SkipUnless(_mongoAvailable, _skipReason);

        var userOptions = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var usersFactory = new Mock<IDbContextFactory<UsersDbContext>>();
        usersFactory.Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new UsersDbContext(userOptions, NullLogger<UsersDbContext>.Instance));

        using var mongoContext = new NebulaMongoContext(
            _testConnectionString,
            _databaseName,
            NullLogger<NebulaMongoContext>.Instance);

        // Seed local Mongo bot token docs, mirroring what the Nebula FTP bot manager
        // writes (index/token/enabled), including one disabled token.
        await mongoContext.UpsertRawTokenDocAsync(
            new BsonDocument { { "index", 1 }, { "token", "backup-token-enabled" }, { "enabled", true } },
            TestContext.Current.CancellationToken);
        await mongoContext.UpsertRawTokenDocAsync(
            new BsonDocument { { "index", 2 }, { "token", "backup-token-disabled" }, { "enabled", false } },
            TestContext.Current.CancellationToken);

        var backupHandler = new RecordingResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) });

        using var backupService = new NebulaSupabaseSyncService(
            mongoContext,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            usersFactory.Object,
            backupHandler);

        var backupResult = await backupService.PerformBackupAsync(
            "https://supabase.invalid",
            "sb_secret_test",
            progressAction: null,
            forceFullSync: true,
            TestContext.Current.CancellationToken);

        Assert.True(backupResult.Success, backupResult.Message);
        Assert.Equal(2, backupResult.BotTokensBackedUp);

        var tokenRequest = Assert.Single(backupHandler.Requests, r => r.Uri.Contains("nebula_bot_tokens", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Post, tokenRequest.Method);
        Assert.Contains("on_conflict=index", tokenRequest.Uri, StringComparison.Ordinal);
        Assert.Contains("\"token\":\"backup-token-enabled\"", tokenRequest.Body, StringComparison.Ordinal);
        Assert.Contains("\"enabled\":true", tokenRequest.Body, StringComparison.Ordinal);
        Assert.Contains("\"token\":\"backup-token-disabled\"", tokenRequest.Body, StringComparison.Ordinal);
        Assert.Contains("\"enabled\":false", tokenRequest.Body, StringComparison.Ordinal);

        // Now simulate a fresh install restoring from the Supabase payload the backup
        // just produced: feed the exact body sent to nebula_bot_tokens back as the
        // GET response, into a separate, empty MongoDB database.
        var restoreHandler = new RecordingResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") }, // nebula_files
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") }, // nebula_users
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") }, // mulletaflix_users
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(tokenRequest.Body!) }); // nebula_bot_tokens

        // MongoDB enforces a 63-byte database name limit; keep the prefix short
        // enough that a full 32-char GUID suffix still fits.
        var restoreDatabaseName = "nebula_t43_bkp_tok_restore_" + Guid.NewGuid().ToString("N");
        using var restoreMongoContext = new NebulaMongoContext(
            _testConnectionString,
            restoreDatabaseName,
            NullLogger<NebulaMongoContext>.Instance);
        using var restoreService = new NebulaSupabaseSyncService(
            restoreMongoContext,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            usersFactory.Object,
            restoreHandler);

        var restoreResult = await restoreService.PerformRestoreAsync(
            "https://supabase.invalid",
            "sb_secret_test",
            progressAction: null,
            forceFullRestore: true,
            TestContext.Current.CancellationToken);

        Assert.True(restoreResult.Success, restoreResult.Message);
        Assert.Equal(2, restoreResult.BotTokensRestored);

        var restoredEnabledTokens = await restoreMongoContext.GetBotTokensAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(["backup-token-enabled"], restoredEnabledTokens);

        _mongoClient!.DropDatabase(restoreDatabaseName);
    }

    public void Dispose()
    {
        if (_mongoAvailable && _mongoClient is not null)
        {
            _mongoClient.DropDatabase(_databaseName);
        }
    }

    private sealed class RecordingResponseHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _nextResponse;

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!.PathAndQuery, body));
            if (request.Method == HttpMethod.Head
                && request.RequestUri.AbsolutePath.EndsWith("/nebula_files", StringComparison.Ordinal))
            {
                var countResponse = new HttpResponseMessage(HttpStatusCode.OK);
                countResponse.Content.Headers.TryAddWithoutValidation("Content-Range", "*/2");
                return countResponse;
            }

            return responses[_nextResponse++];
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string? Body);
}
