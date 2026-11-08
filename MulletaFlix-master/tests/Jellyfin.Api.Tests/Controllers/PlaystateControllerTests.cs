using System;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Database.Implementations.Entities;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class PlaystateControllerTests
{
    private readonly Mock<IUserManager> _userManager = new(MockBehavior.Strict);
    private readonly Mock<IUserDataManager> _userDataRepository = new(MockBehavior.Strict);
    private readonly Mock<ILibraryManager> _libraryManager = new(MockBehavior.Strict);
    private readonly Mock<ISessionManager> _sessionManager = new(MockBehavior.Strict);
    private readonly Mock<ITranscodeManager> _transcodeManager = new(MockBehavior.Strict);

    [Fact]
    public async Task ReportPlaybackStart_NullPayload_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.ReportPlaybackStart(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Playback start info is required.", badRequest.Value);
    }

    [Fact]
    public async Task ReportPlaybackProgress_NullPayload_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.ReportPlaybackProgress(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Playback progress info is required.", badRequest.Value);
    }

    [Fact]
    public async Task ReportPlaybackStopped_NullPayload_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.ReportPlaybackStopped(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Playback stop info is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PingPlaybackSession_NullOrWhitespaceSessionId_ReturnsBadRequest(string? playSessionId)
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.PingPlaybackSession(playSessionId!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Play session id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task MarkPlayedItem_EmptyItemId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.MarkPlayedItem(null, Guid.Empty, null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task MarkUnplayedItem_EmptyItemId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.MarkUnplayedItem(null, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public void RemoveFromResume_EmptyItemId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = controller.RemoveFromResume(null, Guid.Empty);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task OnPlaybackStart_EmptyItemId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.OnPlaybackStart(Guid.Empty, null, null, null, null, null, null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task OnPlaybackProgress_EmptyItemId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.OnPlaybackProgress(Guid.Empty, null, null, null, null, null, null, null, null, null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    [Fact]
    public async Task OnPlaybackStopped_EmptyItemId_ReturnsBadRequest()
    {
        var controller = CreateController(Guid.NewGuid());

        var result = await controller.OnPlaybackStopped(Guid.Empty, null, null, null, null, null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Item id must not be empty.", badRequest.Value);
    }

    private PlaystateController CreateController(Guid userId)
    {
        var claims = new[]
        {
            new Claim(InternalClaimTypes.UserId, userId.ToString("D")),
            new Claim(InternalClaimTypes.DeviceId, "test-device")
        };

        var identity = new ClaimsIdentity(
            claims,
            "test");

        return new PlaystateController(
            _userManager.Object,
            _userDataRepository.Object,
            _libraryManager.Object,
            _sessionManager.Object,
            NullLoggerFactory.Instance,
            _transcodeManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }
}
