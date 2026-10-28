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

    public void Dispose()
    {
        _client?.DropDatabase(_databaseName);
    }

    private NebulaMongoContext CreateContext()
        => new(_connectionString, _databaseName, NullLogger<NebulaMongoContext>.Instance);
}
