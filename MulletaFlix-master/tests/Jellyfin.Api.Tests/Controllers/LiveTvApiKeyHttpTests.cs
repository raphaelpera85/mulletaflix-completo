using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Streaming;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers;

public sealed class LiveTvApiKeyHttpTests
{
    private const string TestScheme = "live-tv-api-key-test";
    private static readonly Guid ChannelId = Guid.Parse("6dcfe2f5-ec33-42f4-955b-9b29f1252b6d");
    private static readonly Guid GenreId = Guid.Parse("1f1a5d46-59e9-4c97-9641-83c1f5d41a60");

    [Fact]
    public async Task Programs_RequiresAuthenticationAndAllowsApiKeyWithoutUserId()
    {
        var liveTvManager = new Mock<ILiveTvManager>();
        InternalItemsQuery? capturedQuery = null;
        liveTvManager
            .Setup(manager => manager.GetPrograms(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<InternalItemsQuery, DtoOptions, CancellationToken>((query, _, _) => capturedQuery = query)
            .ReturnsAsync(new QueryResult<BaseItemDto>());

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(LiveTvController).Assembly);
        builder.Services.AddSingleton(liveTvManager.Object);
        builder.Services.AddSingleton(Mock.Of<IGuideManager>());
        builder.Services.AddSingleton(Mock.Of<ITunerHostManager>());
        builder.Services.AddSingleton(Mock.Of<IListingsManager>());
        builder.Services.AddSingleton(Mock.Of<IRecordingsManager>());
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
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(TestScheme, _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy(
            Policies.LiveTvAccess,
            policy => policy.AddAuthenticationSchemes(TestScheme).RequireAuthenticatedUser()));

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        const string requestUri = "/LiveTv/Programs"
            + "?channelIds=" + "6dcfe2f5-ec33-42f4-955b-9b29f1252b6d"
            + "&sortBy=SortName&sortOrder=Ascending&genres=Drama"
            + "&genreIds=1f1a5d46-59e9-4c97-9641-83c1f5d41a60"
            + "&enableImageTypes=Primary&fields=Overview";

        using (var anonymousResponse = await client.GetAsync(requestUri, TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        }

        client.DefaultRequestHeaders.Add("X-Test-Api-Key", "valid-test-key");
        using var apiKeyResponse = await client.GetAsync(requestUri, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, apiKeyResponse.StatusCode);
        Assert.NotNull(capturedQuery);
        Assert.Null(capturedQuery.User);
        Assert.Equal(ChannelId, Assert.Single(capturedQuery.ChannelIds));
        Assert.Equal(GenreId, Assert.Single(capturedQuery.GenreIds));
        liveTvManager.Verify(
            manager => manager.GetPrograms(
                It.IsAny<InternalItemsQuery>(),
                It.IsAny<DtoOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Api-Key", out var apiKey)
                || !string.Equals(apiKey.ToString(), "valid-test-key", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(InternalClaimTypes.IsApiKey, bool.TrueString) };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
