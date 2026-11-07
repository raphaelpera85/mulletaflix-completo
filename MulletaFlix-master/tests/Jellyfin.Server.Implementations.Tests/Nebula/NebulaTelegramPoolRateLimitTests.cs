using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using WTelegram;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaTelegramPoolRateLimitTests
{
    [Fact]
    public async Task UploadDocumentAsync_RateLimitedBotReleasesLeaseAndNextCallRotates()
    {
        var requests = new List<string>();
        using var handler = new DelegateHttpMessageHandler(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return requests.Count == 1
                ? new HttpResponseMessage((HttpStatusCode)429)
                {
                    Content = new StringContent("{\"ok\":false,\"error_code\":429,\"parameters\":{\"retry_after\":300}}")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"ok\":true,\"result\":{\"message_id\":42,\"chat\":{\"id\":-100123},\"document\":{\"file_id\":\"stored-file\"}}}")
                };
        });

        var sessionsDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nebula-telegram-rate-limit-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var pool = new NebulaTelegramPool(
                12345,
                "test_hash",
                new[] { "bot-zero-token", "bot-one-token" },
                -100123,
                sessionsDirectory,
                NullLogger<NebulaTelegramPool>.Instance,
                new HttpClient(handler));
            var clients = (ConcurrentDictionary<int, Client>)typeof(NebulaTelegramPool)
                .GetField("_clients", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(pool)!;
            // The upload path only checks pool registration; these MTProto clients remain unconnected.
            clients[0] = CreateClient();
            clients[1] = CreateClient();

            var rateLimited = await pool.UploadDocumentAsync(0, [1], "part.bin", "application/octet-stream");

            Assert.Null(rateLimited);
            var botGates = (ConcurrentDictionary<int, SemaphoreSlim>)typeof(NebulaTelegramPool)
                .GetField("_botOperationGates", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(pool)!;
            Assert.Equal(1, botGates[0].CurrentCount);

            var rotated = await pool.UploadDocumentAsync(0, [1], "part.bin", "application/octet-stream");

            Assert.NotNull(rotated);
            Assert.Equal(1, rotated.BotIndex);
            Assert.Equal("stored-file", rotated.FileId);
            Assert.Equal(new[] { "/botbot-zero-token/sendDocument", "/botbot-one-token/sendDocument" }, requests);
            Assert.Equal(1, botGates[1].CurrentCount);
        }
        finally
        {
            if (System.IO.Directory.Exists(sessionsDirectory))
            {
                System.IO.Directory.Delete(sessionsDirectory, recursive: true);
            }
        }
    }

    private static Client CreateClient()
    {
        return new Client(key => key switch
        {
            "api_id" => "12345",
            "api_hash" => "0123456789abcdef0123456789abcdef",
            "bot_token" => "123456:test_token",
            _ => null!
        }, new MemoryStream());
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responseFactory(request));
        }
    }
}
