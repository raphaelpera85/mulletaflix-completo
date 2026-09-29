using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using MulletaFlix.Api.Auth.AnonymousLanAccessPolicy;
using MediaBrowser.Common.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Auth.AnonymousLanAccessPolicy;

public sealed class AnonymousLanAccessHandlerTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("192.168.1.25", true)]
    [InlineData("203.0.113.25", false)]
    public async Task HandleAsync_AllowsLoopbackAndLanButRejectsRemoteAddress(string remoteAddress, bool expectedSuccess)
    {
        var networkManager = new Mock<INetworkManager>();
        networkManager
            .Setup(x => x.IsInLocalNetwork(It.IsAny<IPAddress>()))
            .Returns<IPAddress>(address => address.Equals(IPAddress.Parse("192.168.1.25")));

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(x => x.HttpContext).Returns(httpContext);

        var handler = new AnonymousLanAccessHandler(networkManager.Object, httpContextAccessor.Object);
        var requirement = new AnonymousLanAccessRequirement();
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            new System.Security.Claims.ClaimsPrincipal(),
            resource: null);

        await handler.HandleAsync(context);

        Assert.Equal(expectedSuccess, context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_AllowsMissingRemoteAddressForLoopbackCompatibleHosting()
    {
        var networkManager = new Mock<INetworkManager>();
        var httpContext = new DefaultHttpContext();
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(x => x.HttpContext).Returns(httpContext);

        var handler = new AnonymousLanAccessHandler(networkManager.Object, httpContextAccessor.Object);
        var requirement = new AnonymousLanAccessRequirement();
        var context = new AuthorizationHandlerContext(
            new List<IAuthorizationRequirement> { requirement },
            new System.Security.Claims.ClaimsPrincipal(),
            resource: null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
        networkManager.Verify(x => x.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }
}
