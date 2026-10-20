using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers;

public sealed class LiveRecordingStreamCapabilityHttpTests
{
    private const string TestScheme = "recording-capability-test";

    [Fact]
    public async Task AnonymousRequestWithActiveCapabilityStreamsRecordingWithoutCaching()
    {
        const string capability = "opaque-active-recording-capability";
        var expectedContent = new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0x00, 0xFF };
        var filePath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(filePath, expectedContent, TestContext.Current.CancellationToken);

        try
        {
            var recordings = new Mock<IRecordingsManager>();
            recordings.Setup(manager => manager.GetActiveRecordingStreamPath(It.IsAny<string>())).Returns((string?)null);
            recordings.Setup(manager => manager.GetActiveRecordingStreamPath(capability)).Returns(filePath);
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(LiveTvController).Assembly);
            builder.Services.AddSingleton(recordings.Object);
            builder.Services.AddSingleton(Mock.Of<ILiveTvManager>());
            builder.Services.AddSingleton(Mock.Of<IGuideManager>());
            builder.Services.AddSingleton(Mock.Of<ITunerHostManager>());
            builder.Services.AddSingleton(Mock.Of<IListingsManager>());
            builder.Services.AddSingleton(Mock.Of<IUserManager>());
            builder.Services.AddSingleton(Mock.Of<ILibraryManager>());
            builder.Services.AddSingleton(Mock.Of<IDtoService>());
            builder.Services.AddSingleton(Mock.Of<IMediaSourceManager>());
            builder.Services.AddSingleton(Mock.Of<ITranscodeManager>());
            builder.Services.AddSingleton(Mock.Of<ISchedulesDirectService>());
            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestScheme;
                    options.DefaultChallengeScheme = TestScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, AnonymousTestAuthenticationHandler>(TestScheme, _ => { });
            builder.Services.AddAuthorization(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder(TestScheme).RequireAuthenticatedUser().Build());

            await using var application = builder.Build();
            HttpContext? activeRequest = null;
            var requestCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            application.Use(async (context, next) =>
            {
                activeRequest = context;
                try
                {
                    await next();
                }
                finally
                {
                    requestCompleted.TrySetResult();
                }
            });
            application.UseRouting();
            application.UseAuthentication();
            application.UseAuthorization();
            application.MapControllers();
            await application.StartAsync(TestContext.Current.CancellationToken);

            using var client = application.GetTestClient();
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/LiveTv/LiveRecordings/{Uri.EscapeDataString(capability)}/stream");
            using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                requestCancellation.Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore == true);
            await using var responseBody = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
            var actualContent = new byte[expectedContent.Length];
            await responseBody.ReadExactlyAsync(actualContent, TestContext.Current.CancellationToken);
            Assert.Equal(expectedContent, actualContent);
            recordings.Verify(manager => manager.GetActiveRecordingStreamPath(capability), Times.Once);
            recordings.Verify(manager => manager.GetActiveRecordingPath(It.IsAny<string>()), Times.Never);

            // A live recording stream remains open until the client cancels it. Close the
            // response explicitly before deleting its backing file on Windows.
            activeRequest?.Abort();
            requestCancellation.Cancel();
            await responseBody.DisposeAsync();
            response.Dispose();
            request.Dispose();
            await requestCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await application.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task AnonymousRequestsWithTimerIdsAndMalformedCapabilitiesReturnNotFound()
    {
        var recordings = new Mock<IRecordingsManager>();
        recordings.Setup(manager => manager.GetActiveRecordingStreamPath(It.IsAny<string>())).Returns((string?)null);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(LiveTvController).Assembly);
        builder.Services.AddSingleton(recordings.Object);
        builder.Services.AddSingleton(Mock.Of<ILiveTvManager>());
        builder.Services.AddSingleton(Mock.Of<IGuideManager>());
        builder.Services.AddSingleton(Mock.Of<ITunerHostManager>());
        builder.Services.AddSingleton(Mock.Of<IListingsManager>());
        builder.Services.AddSingleton(Mock.Of<IUserManager>());
        builder.Services.AddSingleton(Mock.Of<ILibraryManager>());
        builder.Services.AddSingleton(Mock.Of<IDtoService>());
        builder.Services.AddSingleton(Mock.Of<IMediaSourceManager>());
        builder.Services.AddSingleton(Mock.Of<ITranscodeManager>());
        builder.Services.AddSingleton(Mock.Of<ISchedulesDirectService>());
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestScheme;
                options.DefaultChallengeScheme = TestScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, AnonymousTestAuthenticationHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization();

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        var suppliedIds = new[]
        {
            "timer-id-known-to-clients",
            "00000000000000000000000000000000",
            "not-a-guid",
            "..",
            new string('a', 2048)
        };

        foreach (var suppliedId in suppliedIds)
        {
            using var response = await client.GetAsync(
                $"/LiveTv/LiveRecordings/{Uri.EscapeDataString(suppliedId)}/stream",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        const int fuzzSampleCount = 256;
        const string tokenAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";
        var random = new Random(0x5EED);
        for (var sample = 0; sample < fuzzSampleCount; sample++)
        {
            var token = new char[random.Next(1, 129)];
            for (var index = 0; index < token.Length; index++)
            {
                token[index] = tokenAlphabet[random.Next(tokenAlphabet.Length)];
            }

            var suppliedId = new string(token);
            using var response = await client.GetAsync(
                $"/LiveTv/LiveRecordings/{Uri.EscapeDataString(suppliedId)}/stream",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        recordings.Verify(
            manager => manager.GetActiveRecordingStreamPath(It.IsAny<string>()),
            Times.Exactly(suppliedIds.Length - 1 + fuzzSampleCount));
        recordings.Verify(manager => manager.GetActiveRecordingStreamPath(".."), Times.Never);
        recordings.Verify(manager => manager.GetActiveRecordingPath(It.IsAny<string>()), Times.Never);
    }

    private sealed class AnonymousTestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AnonymousTestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());
    }
}
