using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Providers.Plugins.ListenBrainz.Api;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.ListenBrainz;

public sealed class ListenBrainzLabsClientTests
{
    [Fact]
    public async Task GetSimilarArtistsAsync_RedactsUrlFromTransportExceptionLog()
    {
        const string sensitiveUrl = "https://alice:secret@provider.example/path?api_key=token";
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>((_, _) =>
                Task.FromException<HttpResponseMessage>(new HttpRequestException($"Request failed at {sensitiveUrl}")));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler.Object));
        var logger = new CapturingLogger<ListenBrainzLabsClient>();
        using var client = new ListenBrainzLabsClient(factory.Object, logger);

        var results = await client.GetSimilarArtistsAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(results);
        var logs = string.Join(Environment.NewLine, logger.Messages);
        Assert.Contains("[REDACTED_URL]", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("alice", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key=token", logs, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            where TState : notnull
            => Messages.Add(formatter(state, exception));
    }
}
