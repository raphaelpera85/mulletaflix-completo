using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaSegments;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.MediaSegments;
using MediaBrowser.Model.Querying;
using MulletaFlix.Database.Implementations.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class MediaSegmentsControllerAuthorizationTests
{
    [Fact]
    public async Task GetItemSegments_AnonymousRequestIsRejectedThroughHttpPipeline()
    {
        const string testScheme = "MediaSegmentsTest";
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(MediaSegmentsController).Assembly);
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = testScheme;
                options.DefaultChallengeScheme = testScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, AnonymousAuthenticationHandler>(testScheme, _ => { });
        builder.Services.AddAuthorization();

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthentication();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        using var response = await client.GetAsync($"/MediaSegments/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetItemSegments_ItemNotVisibleToRequestUserReturnsNotFoundWithoutQueryingSegments()
    {
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, userId)).Returns((BaseItem?)null);
        var segmentManager = new Mock<IMediaSegmentManager>(MockBehavior.Strict);
        var controller = CreateController(userId, libraryManager.Object, segmentManager.Object);

        var result = await controller.GetItemSegments(itemId);

        Assert.IsType<NotFoundResult>(result.Result);
        libraryManager.Verify(manager => manager.GetItemById<BaseItem>(itemId, userId), Times.Once);
        segmentManager.Verify(
            manager => manager.GetSegmentsAsync(
                It.IsAny<BaseItem>(),
                It.IsAny<IEnumerable<MediaSegmentType>?>(),
                It.IsAny<LibraryOptions>(),
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task GetItemSegments_VisibleItemUsesRequestUserAndReturnsItsSegments()
    {
        var itemId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var item = new Mock<BaseItem>().Object;
        var options = new LibraryOptions();
        var expectedSegment = new MediaSegmentDto { ItemId = itemId };
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, userId)).Returns(item);
        libraryManager.Setup(manager => manager.GetLibraryOptions(item)).Returns(options);
        var segmentManager = new Mock<IMediaSegmentManager>();
        segmentManager.Setup(manager => manager.GetSegmentsAsync(item, null, options, true))
            .ReturnsAsync([expectedSegment]);
        var controller = CreateController(userId, libraryManager.Object, segmentManager.Object);

        var result = await controller.GetItemSegments(itemId);

        var response = Assert.IsAssignableFrom<OkObjectResult>(result.Result);
        var query = Assert.IsType<QueryResult<MediaSegmentDto>>(response.Value);
        Assert.Same(expectedSegment, Assert.Single(query.Items));
        libraryManager.Verify(manager => manager.GetItemById<BaseItem>(itemId, userId), Times.Once);
        segmentManager.Verify(manager => manager.GetSegmentsAsync(item, null, options, true), Times.Once);
    }

    private static MediaSegmentsController CreateController(
        Guid userId,
        ILibraryManager libraryManager,
        IMediaSegmentManager segmentManager)
    {
        return new MediaSegmentsController(segmentManager, libraryManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(InternalClaimTypes.UserId, userId.ToString("N"))],
                        "TestAuth"))
                }
            }
        };
    }

    private sealed class AnonymousAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AnonymousAuthenticationHandler(
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
