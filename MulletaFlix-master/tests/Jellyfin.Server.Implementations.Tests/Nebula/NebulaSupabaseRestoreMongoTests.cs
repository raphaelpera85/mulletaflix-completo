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
/// Verifies Supabase restore persistence against a dedicated MongoDB test instance.
/// </summary>
[Trait("Category", "RequiresMongo")]
public sealed class NebulaSupabaseRestoreMongoTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _databaseName = "nebula_t43_restore_" + Guid.NewGuid().ToString("N");
    private readonly string _testConnectionString;
    private readonly MongoClient? _mongoClient;
    private readonly bool _mongoAvailable;
    private readonly string _skipReason;

    public NebulaSupabaseRestoreMongoTests(ITestOutputHelper output)
    {
        _output = output;
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
        catch (Exception)
        {
            _mongoAvailable = false;
            _skipReason = "MongoDB de teste indisponível no destino local opt-in.";
        }
    }

    [Fact]
    public async Task Restore_PersistsSupabaseUsersAndBotTokensIntoIsolatedStores()
    {
        Assert.SkipUnless(_mongoAvailable, _skipReason);

        var userOptions = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var usersFactory = new Mock<IDbContextFactory<UsersDbContext>>();
        usersFactory.Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new UsersDbContext(userOptions, NullLogger<UsersDbContext>.Instance));

        var appUserId = Guid.NewGuid();
        var handler = new RestoreResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "[{\"doc_data\":{\"_id\":\"restore-ftp-user\",\"password_hash\":\"ftp-hash-fixture\",\"permissions\":\"elradfmwM\",\"fixture_marker\":\"preserved\"}}]")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"[{{\"id\":\"{appUserId:D}\",\"username\":\"restore-app-user\",\"normalized_username\":\"RESTORE-APP-USER\",\"password\":\"app-hash-fixture\",\"authentication_provider_id\":\"default\",\"password_reset_provider_id\":\"default\",\"enable_local_password\":true,\"enable_user_preference_access\":true,\"permissions\":[],\"license\":null}}]")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"index\":2,\"token\":\"restore-bot-token-fixture\",\"enabled\":true}]")
            });

        using var mongoContext = new NebulaMongoContext(
            _testConnectionString,
            _databaseName,
            NullLogger<NebulaMongoContext>.Instance);
        using var service = new NebulaSupabaseSyncService(
            mongoContext,
            NullLogger<NebulaSupabaseSyncService>.Instance,
            usersFactory.Object,
            handler);

        var result = await service.PerformRestoreAsync(
            "https://supabase.invalid",
            "sb_secret_test",
            progressAction: null,
            forceFullRestore: true,
            TestContext.Current.CancellationToken);
        Assert.True(result.Success, result.Message);
        Assert.Equal(0, result.FilesRestored);
        Assert.Equal(2, result.UsersRestored);
        Assert.True(double.IsFinite(result.ElapsedSeconds));
        _output.WriteLine(
            "Restore Supabase→Mongo isolado: restoredUsers={0}; restoredFiles={1}; elapsedSeconds={2:F3}",
            result.UsersRestored,
            result.FilesRestored,
            result.ElapsedSeconds);
        Assert.NotNull(service.LastSuccessfulRestoreTime);
        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("apikey", request.Headers.Keys);
            Assert.Equal("Bearer sb_secret_test", request.Headers["Authorization"]);
        });
        Assert.Contains("/rest/v1/nebula_users?select=*", handler.Requests[0].Uri, StringComparison.Ordinal);
        Assert.Contains("/rest/v1/mulletaflix_users?select=*&order=username.asc", handler.Requests[1].Uri, StringComparison.Ordinal);
        Assert.Contains("/rest/v1/nebula_bot_tokens?select=*&order=index.asc", handler.Requests[2].Uri, StringComparison.Ordinal);

        var ftpUsers = await mongoContext.GetAllUsersForSyncAsync(TestContext.Current.CancellationToken);
        var ftpUser = Assert.Single(ftpUsers);
        Assert.Equal("restore-ftp-user", ftpUser["_id"].AsString);
        Assert.Equal("ftp-hash-fixture", ftpUser["password_hash"].AsString);
        Assert.Equal("preserved", ftpUser["fixture_marker"].AsString);
        Assert.Equal(1, await mongoContext.CountUsersAsync(TestContext.Current.CancellationToken));

        var botTokens = await mongoContext.GetBotTokensAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(["restore-bot-token-fixture"], botTokens);

        await using var usersContext = new UsersDbContext(userOptions, NullLogger<UsersDbContext>.Instance);
        var restoredAppUser = await usersContext.Users.SingleAsync(
            user => user.Id == appUserId,
            TestContext.Current.CancellationToken);
        Assert.Equal("restore-app-user", restoredAppUser.Username);
        Assert.Equal("RESTORE-APP-USER", restoredAppUser.NormalizedUsername);
        Assert.Equal("app-hash-fixture", restoredAppUser.Password);
    }

    public void Dispose()
    {
        if (_mongoAvailable && _mongoClient is not null)
        {
            try
            {
                _mongoClient.DropDatabase(_databaseName);
            }
            catch (Exception)
            {
                // This test owns only its unique database; cleanup failure must not mask test results.
            }
        }
    }

    private sealed class RestoreResponseHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _nextResponse;

        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers)
            {
                headers[header.Key] = string.Join(",", header.Value);
            }

            Requests.Add(new CapturedRequest(request.Method, request.RequestUri!.PathAndQuery, headers));
            return Task.FromResult(responses[_nextResponse++]);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, IReadOnlyDictionary<string, string> Headers);
}
