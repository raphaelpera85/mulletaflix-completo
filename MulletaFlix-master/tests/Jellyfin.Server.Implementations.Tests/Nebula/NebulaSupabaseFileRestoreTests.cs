using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaSupabaseFileRestoreTests
{
    [Fact]
    public async Task RestoreFilesFromSupabaseAsync_PaginatesAndMapsMongoFields()
    {
        var rows = Enumerable.Range(0, NebulaSupabaseSyncService.SupabaseRestorePageSize)
            .Select(index => CreateRow($"file-{index:D4}", $"media-{index}.mkv"));
        var firstPage = "[" + string.Join(',', rows) + "]";
        var secondPage = "[" + CreateRow("file-last", "last.mkv") + "]";
        var handler = new QueueResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(firstPage) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(secondPage) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });

        using var mongo = new NebulaMongoContext(
            "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=100",
            "nebula_restore_unit_" + Guid.NewGuid().ToString("N"),
            NullLogger<NebulaMongoContext>.Instance);
        using var service = new NebulaSupabaseSyncService(mongo, NullLogger<NebulaSupabaseSyncService>.Instance, null!, handler);
        var restored = new List<BsonDocument>();

        var count = await service.RestoreFilesFromSupabaseAsync(
            "https://supabase.invalid",
            "sb_secret_fixture",
            (batch, _) =>
            {
                restored.AddRange(batch);
                return Task.FromResult(batch.Count);
            },
            progressAction: null,
            CancellationToken.None);

        Assert.Equal(NebulaSupabaseSyncService.SupabaseRestorePageSize + 1, count);
        Assert.Equal(count, restored.Count);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("offset=0", handler.Requests[0].Uri, StringComparison.Ordinal);
        Assert.Contains($"offset={NebulaSupabaseSyncService.SupabaseRestorePageSize}", handler.Requests[1].Uri, StringComparison.Ordinal);
        Assert.Contains($"offset={NebulaSupabaseSyncService.SupabaseRestorePageSize + 1}", handler.Requests[2].Uri, StringComparison.Ordinal);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("sb_secret_fixture", request.ApiKey);
            Assert.Equal("Bearer sb_secret_fixture", request.Authorization);
        });

        var first = Assert.Single(restored.Where(item => item["_id"] == "file-0000"));
        Assert.Equal("media-0.mkv", first["name"].AsString);
        Assert.Equal("completed", first["status"].AsString);
        Assert.Equal(123L, first["size"].ToInt64());
        Assert.Equal(77L, first["parts"][0]["message_id"].ToInt64());
        Assert.Equal("poster-fixture", first["local_poster"].AsString);
    }

    [Fact]
    public async Task RestoreFilesFromSupabaseAsync_ContinuesAfterServerCapsPageBelowRequestedLimit()
    {
        var cappedPage = "[" + string.Join(',', Enumerable.Range(0, 100)
            .Select(index => CreateRow($"capped-{index:D3}", $"media-{index}.mkv"))) + "]";
        var handler = new QueueResponseHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(cappedPage) },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[" + CreateRow("capped-last", "last.mkv") + "]") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });

        using var mongo = new NebulaMongoContext(
            "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=100",
            "nebula_restore_unit_" + Guid.NewGuid().ToString("N"),
            NullLogger<NebulaMongoContext>.Instance);
        using var service = new NebulaSupabaseSyncService(mongo, NullLogger<NebulaSupabaseSyncService>.Instance, null!, handler);
        var persisted = new List<BsonDocument>();

        var count = await service.RestoreFilesFromSupabaseAsync(
            "https://supabase.invalid",
            "sb_secret_fixture",
            (batch, _) =>
            {
                persisted.AddRange(batch);
                return Task.FromResult(batch.Count);
            },
            progressAction: null,
            CancellationToken.None);

        Assert.Equal(101, count);
        Assert.Equal(101, persisted.Count);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Contains("offset=0", handler.Requests[0].Uri, StringComparison.Ordinal);
        Assert.Contains("offset=100", handler.Requests[1].Uri, StringComparison.Ordinal);
        Assert.Contains("offset=101", handler.Requests[2].Uri, StringComparison.Ordinal);
        Assert.Contains(persisted, item => item["_id"] == "capped-last");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[{}]")]
    [InlineData("[{\"id\":\"file-1\",\"doc_data\":null}]")]
    public async Task RestoreFilesFromSupabaseAsync_RejectsInvalidPayload(string payload)
    {
        var handler = new QueueResponseHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload)
        });

        using var mongo = new NebulaMongoContext(
            "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=100",
            "nebula_restore_unit_" + Guid.NewGuid().ToString("N"),
            NullLogger<NebulaMongoContext>.Instance);
        using var service = new NebulaSupabaseSyncService(mongo, NullLogger<NebulaSupabaseSyncService>.Instance, null!, handler);
        var persistCalls = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreFilesFromSupabaseAsync(
            "https://supabase.invalid",
            "sb_secret_fixture",
            (_, _) =>
            {
                persistCalls++;
                return Task.FromResult(0);
            },
            progressAction: null,
            CancellationToken.None));

        Assert.Equal(0, persistCalls);
    }

    [Fact]
    public async Task RestoreFilesFromSupabaseAsync_RejectsHttpFailure()
    {
        var handler = new QueueResponseHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var mongo = new NebulaMongoContext(
            "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=100",
            "nebula_restore_unit_" + Guid.NewGuid().ToString("N"),
            NullLogger<NebulaMongoContext>.Instance);
        using var service = new NebulaSupabaseSyncService(mongo, NullLogger<NebulaSupabaseSyncService>.Instance, null!, handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreFilesFromSupabaseAsync(
            "https://supabase.invalid",
            "sb_secret_fixture",
            (_, _) => Task.FromResult(0),
            progressAction: null,
            CancellationToken.None));
    }

    private static string CreateRow(string id, string name)
    {
        var escapedId = System.Text.Json.JsonSerializer.Serialize(id);
        var escapedName = System.Text.Json.JsonSerializer.Serialize(name);
        return "{"
            + "\"id\":" + escapedId + ","
            + "\"name\":" + escapedName + ","
            + "\"parent\":null,\"size\":123,\"status\":\"completed\","
            + "\"parts\":[{\"message_id\":77}],\"uploaded_at\":1700000000,"
            + "\"doc_data\":{\"_id\":" + escapedId + ",\"name\":" + escapedName + ",\"local_poster\":\"poster-fixture\"}"
            + "}";
    }

    private sealed class QueueResponseHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _next;

        public List<CapturedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!.PathAndQuery,
                request.Headers.GetValues("apikey").Single(),
                request.Headers.Authorization?.ToString() ?? string.Empty));
            return Task.FromResult(responses[_next++]);
        }
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, string ApiKey, string Authorization);
}
