using System;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Verifica a topologia MongoDB antes de qualquer decisão sobre Change Streams.
/// </summary>
/// <remarks>
/// T5.5 exige explicitamente conferir a topologia primeiro: Change Streams
/// dependem de replica set ou cluster fragmentado e falham em standalone. Este
/// teste transforma essa verificação em código executável, em vez de uma
/// suposição no documento — se a topologia mudar no futuro, o resultado muda com
/// ela. O teste é informativo e não falha por standalone, porque standalone é uma
/// configuração legítima: ele falha se o suporte for detectado de forma
/// inconsistente.
/// </remarks>
[Trait("Category", "RequiresMongo")]
public sealed class MongoTopologyCapabilityTests
{
    private const string TestConnectionString = "mongodb://127.0.0.1:27099/?serverSelectionTimeoutMS=1500";

    [Fact]
    public async Task ChangeStreamSupport_MatchesTheReportedTopology()
    {
        MongoClient client;
        BsonDocument helloResult;
        try
        {
            var settings = MongoClientSettings.FromConnectionString(TestConnectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
            client = new MongoClient(settings);
            helloResult = await client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: TestContext.Current.CancellationToken);
        }
        catch (Exception)
        {
            Assert.Skip("MongoDB de teste indisponível em 127.0.0.1:27099.");
            return;
        }

        // `setName` presente indica replica set; `msg: isdbgrid` indica mongos.
        var isReplicaSet = helloResult.Contains("setName");
        var isSharded = helloResult.TryGetValue("msg", out var msg)
            && string.Equals(msg.AsString, "isdbgrid", StringComparison.Ordinal);
        var supportsChangeStreams = isReplicaSet || isSharded;

        // A verificação empírica: abrir um Change Stream só pode ter sucesso
        // quando a topologia o suporta. Um standalone rejeita com erro do
        // servidor, e é justamente por isso que a decisão de T5.5 não pode ser
        // tomada no papel.
        var database = client.GetDatabase("mulletaflix_topology_probe");
        var collection = database.GetCollection<BsonDocument>("probe");
        var changeStreamWorked = false;
        try
        {
            using var cursor = await collection.WatchAsync(
                cancellationToken: TestContext.Current.CancellationToken);
            changeStreamWorked = true;
        }
        catch (MongoCommandException)
        {
            changeStreamWorked = false;
        }
        catch (NotSupportedException)
        {
            changeStreamWorked = false;
        }
        finally
        {
            try
            {
                await client.DropDatabaseAsync(
                    "mulletaflix_topology_probe",
                    TestContext.Current.CancellationToken);
            }
            catch (Exception)
            {
                // Banco de sondagem descartável.
            }
        }

        // O invariante que importa: a capacidade anunciada pela topologia e o
        // comportamento real precisam coincidir. Divergência aqui significaria
        // que a decisão de arquitetura foi tomada sobre premissa falsa.
        Assert.Equal(supportsChangeStreams, changeStreamWorked);

        // Registra o achado para que a decisão de T5.5 fique rastreável.
        var topology = isReplicaSet ? "replica set" : isSharded ? "sharded" : "standalone";
        Assert.False(
            supportsChangeStreams && !changeStreamWorked,
            $"Topologia {topology} anuncia suporte a Change Streams mas a abertura falhou.");
    }

    [Fact]
    public async Task ProductionTopology_IsInspectedWithoutBeingModified()
    {
        // T5.5 exige verificar a topologia da instalação real antes de planejar
        // Change Streams. Esta é uma leitura pura: apenas o comando `hello`, sem
        // escrita, sem abrir Change Stream e sem tocar em dados.
        BsonDocument hello;
        try
        {
            var settings = MongoClientSettings.FromConnectionString(
                "mongodb://127.0.0.1:27017/?serverSelectionTimeoutMS=1500");
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(2);
            var client = new MongoClient(settings);
            hello = await client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(
                    new BsonDocument("hello", 1),
                    cancellationToken: TestContext.Current.CancellationToken);
        }
        catch (Exception)
        {
            Assert.Skip("MongoDB de produção indisponível em 127.0.0.1:27017.");
            return;
        }

        var isReplicaSet = hello.Contains("setName");
        var isSharded = hello.TryGetValue("msg", out var msg)
            && string.Equals(msg.AsString, "isdbgrid", StringComparison.Ordinal);

        // Não afirma qual topologia deve ser: afirma que a resposta é
        // interpretável. Standalone é uma configuração legítima, e é justamente
        // esse fato que determina a decisão de manter polling em vez de Change
        // Streams.
        Assert.True(hello.Contains("ok"), "A instância deve responder ao comando hello.");
        Assert.False(
            isReplicaSet && isSharded,
            "Uma instância não pode ser simultaneamente replica set e mongos.");
    }
}
