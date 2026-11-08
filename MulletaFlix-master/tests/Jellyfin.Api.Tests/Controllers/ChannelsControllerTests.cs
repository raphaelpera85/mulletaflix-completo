using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Database.Implementations.Entities;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class ChannelsControllerTests
{
    private readonly Mock<IChannelManager> _channelManager;
    private readonly Mock<IUserManager> _userManager;
    private readonly Guid _userId;
    private readonly User _testUser;
    private readonly ChannelsController _controller;

    public ChannelsControllerTests()
    {
        _channelManager = new Mock<IChannelManager>(MockBehavior.Loose);
        _userManager = new Mock<IUserManager>(MockBehavior.Loose);
        _userId = Guid.NewGuid();
        _testUser = new User("channelTester", "testAuth", "testReset");

        _userManager.Setup(m => m.GetUserById(_userId)).Returns(_testUser);

        _controller = new ChannelsController(_channelManager.Object, _userManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
                            new Claim(ClaimTypes.Role, UserRoles.Administrator)
                        },
                        "Custom"))
                }
            }
        };
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public async Task GetChannels_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var result = await _controller.GetChannels(_userId, startIndex, null, null, null, null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetChannels_NegativeLimit_ReturnsBadRequest(int limit)
    {
        var result = await _controller.GetChannels(_userId, null, limit, null, null, null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void GetChannelFeatures_EmptyChannelId_ReturnsBadRequest()
    {
        var result = _controller.GetChannelFeatures(Guid.Empty);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public void GetChannelFeatures_NotFound_ReturnsNotFound()
    {
        var channelId = Guid.NewGuid();
        _channelManager.Setup(m => m.GetChannelFeatures(channelId)).Returns((ChannelFeatures?)null);

        var result = _controller.GetChannelFeatures(channelId);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetChannelItems_EmptyChannelId_ReturnsBadRequest()
    {
        var result = await _controller.GetChannelItems(Guid.Empty, null, _userId, null, null, [], [], [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task GetChannelItems_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var result = await _controller.GetChannelItems(Guid.NewGuid(), null, _userId, startIndex, null, [], [], [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-25)]
    public async Task GetChannelItems_NegativeLimit_ReturnsBadRequest(int limit)
    {
        var result = await _controller.GetChannelItems(Guid.NewGuid(), null, _userId, null, limit, [], [], [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task GetLatestChannelItems_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var result = await _controller.GetLatestChannelItems(_userId, startIndex, null, [], [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-25)]
    public async Task GetLatestChannelItems_NegativeLimit_ReturnsBadRequest(int limit)
    {
        var result = await _controller.GetLatestChannelItems(_userId, null, limit, [], [], []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}
