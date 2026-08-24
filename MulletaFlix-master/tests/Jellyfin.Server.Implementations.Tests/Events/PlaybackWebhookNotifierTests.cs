using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Events.Consumers.Session;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Events;

public class PlaybackWebhookNotifierTests
{
    private static PlaybackStartEventArgs CreateEventArgs(string itemName)
    {
        var user = new User("alice", "default", "default");
        return new PlaybackStartEventArgs
        {
            Users = new System.Collections.Generic.List<User> { user },
            MediaInfo = new BaseItemDto
            {
                Name = itemName,
                Type = BaseItemKind.Movie,
                SeriesName = null,
                Overview = null
            },
            DeviceName = "Chrome",
            ClientName = "Dashboard"
        };
    }

    [Fact]
    public async Task OnEvent_NoWebhookUrlConfigured_DoesNotPost()
    {
        Environment.SetEnvironmentVariable("MulletaFlix_WEBHOOK_URL", null);
        Environment.SetEnvironmentVariable("MulletaFlix_WEBHOOK_EVENTS", null);

        var http = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<PlaybackWebhookNotifier>>();
        var notifier = new PlaybackWebhookNotifier(logger, http.Object);

        await notifier.OnEvent(CreateEventArgs("Test Movie"));

        http.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnEvent_DisabledEvent_DoesNotPost()
    {
        Environment.SetEnvironmentVariable("MulletaFlix_WEBHOOK_URL", "http://example.com/hook");
        Environment.SetEnvironmentVariable("MulletaFlix_WEBHOOK_EVENTS", "PlaybackStop");

        var http = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var logger = Mock.Of<ILogger<PlaybackWebhookNotifier>>();
        var notifier = new PlaybackWebhookNotifier(logger, http.Object);

        await notifier.OnEvent(CreateEventArgs("Test Movie"));

        http.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnEvent_EnabledEvent_PostsJsonPayload()
    {
        Environment.SetEnvironmentVariable("MulletaFlix_WEBHOOK_URL", "http://example.com/hook");
        Environment.SetEnvironmentVariable("MulletaFlix_WEBHOOK_EVENTS", "PlaybackStart");

        var handler = new RecordingHandler();
        var client = new HttpClient(handler);
        var http = new Mock<IHttpClientFactory>();
        http.Setup(h => h.CreateClient(It.IsAny<string>())).Returns(client);

        var logger = Mock.Of<ILogger<PlaybackWebhookNotifier>>();
        var notifier = new PlaybackWebhookNotifier(logger, http.Object);

        await notifier.OnEvent(CreateEventArgs("Test Movie"));

        Assert.Single(handler.Requests);
        Assert.Contains("\"event\":\"PlaybackStart\"", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"item\":\"Test Movie\"", handler.Bodies[0], StringComparison.Ordinal);
        Assert.Contains("\"user\":\"alice\"", handler.Bodies[0], StringComparison.Ordinal);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public System.Collections.Generic.List<HttpRequestMessage> Requests { get; } = new();
        public System.Collections.Generic.List<string> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
