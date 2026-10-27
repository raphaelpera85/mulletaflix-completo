using System;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using MulletaFlix.Api.Auth.LocalAccessOrRequiresElevationPolicy;
using MulletaFlix.Api.Constants;
using Xunit;

namespace MulletaFlix.Api.Tests.Auth;

public sealed class LocalAccessOrRequiresElevationHandlerTests
{
    [Theory]
    [InlineData("127.0.0.1", true, null, true)]
    [InlineData("192.168.1.25", true, null, true)]
    [InlineData("192.168.1.25", true, "User", true)]
    [InlineData(null, true, null, true)]
    [InlineData("203.0.113.10", false, null, false)]
    [InlineData("203.0.113.10", false, "User", false)]
    [InlineData("203.0.113.10", false, "Administrator", true)]
    public async Task Handler_AllowsOnlyLocalAccessOrRemoteAdministrator(
        string? remoteIp,
        bool isLocalNetwork,
        string? role,
        bool expectedSuccess)
    {
        var networkManager = new Mock<INetworkManager>();
        networkManager.Setup(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>())).Returns(isLocalNetwork);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        if (remoteIp is not null)
        {
            accessor.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        }

        var claims = role is null ? Array.Empty<Claim>() : [new Claim(ClaimTypes.Role, role)];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var requirement = new LocalAccessOrRequiresElevationRequirement();
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        var handler = new LocalAccessOrRequiresElevationHandler(networkManager.Object, accessor);

        await handler.HandleAsync(context);

        Assert.Equal(expectedSuccess, context.HasSucceeded);
        networkManager.Verify(
            manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()),
            Times.Once);
    }

    [Fact]
    public async Task Handler_AllowsMissingHttpContext()
    {
        var networkManager = new Mock<INetworkManager>(MockBehavior.Strict);
        var requirement = new LocalAccessOrRequiresElevationRequirement();
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(), null);
        var handler = new LocalAccessOrRequiresElevationHandler(networkManager.Object, new HttpContextAccessor());

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        networkManager.VerifyNoOtherCalls();
    }
}
