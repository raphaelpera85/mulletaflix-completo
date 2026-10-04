using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class ChannelsControllerAuthorizationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetChannels_AuthenticatedIdentityWithoutResolvedUser_ReturnsUnauthorized(bool includeUserIdClaim)
    {
        var (controller, channelManager) = CreateController(includeUserIdClaim: includeUserIdClaim);

        var result = await controller.GetChannels(null, null, null, null, null, null);

        Assert.IsType<UnauthorizedResult>(result.Result);
        channelManager.Verify(manager => manager.GetChannelsAsync(It.IsAny<ChannelQuery>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetChannelItems_AuthenticatedIdentityWithoutResolvedUser_ReturnsUnauthorized(bool includeUserIdClaim)
    {
        var (controller, channelManager) = CreateController(includeUserIdClaim: includeUserIdClaim);

        var result = await controller.GetChannelItems(
            Guid.NewGuid(), null, null, null, null, [], [], [], []);

        Assert.IsType<UnauthorizedResult>(result.Result);
        channelManager.Verify(manager => manager.GetChannelItems(It.IsAny<InternalItemsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetLatestChannelItems_AuthenticatedIdentityWithoutResolvedUser_ReturnsUnauthorized(bool includeUserIdClaim)
    {
        var (controller, channelManager) = CreateController(includeUserIdClaim: includeUserIdClaim);

        var result = await controller.GetLatestChannelItems(null, null, null, [], [], []);

        Assert.IsType<UnauthorizedResult>(result.Result);
        channelManager.Verify(manager => manager.GetLatestChannelItems(It.IsAny<InternalItemsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetChannels_ApiKeyWithoutUser_RemainsSupported()
    {
        var (controller, channelManager) = CreateController(isApiKey: true);
        channelManager
            .Setup(manager => manager.GetChannelsAsync(It.IsAny<ChannelQuery>()))
            .ReturnsAsync(new QueryResult<BaseItemDto>());

        var result = await controller.GetChannels(null, null, null, null, null, null);

        Assert.NotNull(result.Value);
        channelManager.Verify(manager => manager.GetChannelsAsync(It.Is<ChannelQuery>(query => query.UserId == Guid.Empty)), Times.Once);
    }

    private static (ChannelsController Controller, Mock<IChannelManager> ChannelManager) CreateController(
        bool isApiKey = false,
        bool includeUserIdClaim = true)
    {
        var userId = Guid.NewGuid();
        var users = new Mock<IUserManager>();
        users.Setup(manager => manager.GetUserById(userId)).Returns((User?)null);
        var channels = new Mock<IChannelManager>();
        var claims = new List<Claim>();
        if (isApiKey)
        {
            claims.Add(new Claim(InternalClaimTypes.IsApiKey, bool.TrueString));
        }
        else if (includeUserIdClaim)
        {
            claims.Add(new Claim(InternalClaimTypes.UserId, userId.ToString("N")));
        }

        var controller = new ChannelsController(channels.Object, users.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            }
        };

        return (controller, channels);
    }
}
