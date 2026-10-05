using System;
using System.Net;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
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

        recordings.Verify(manager => manager.GetActiveRecordingStreamPath(It.IsAny<string>()), Times.Exactly(suppliedIds.Length - 1));
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
