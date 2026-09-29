using System.Net;
using System.Threading.Tasks;
using MulletaFlix.Server.ServerSetupApp;
using MediaBrowser.Common.Net;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Tests.ServerSetupApp;

public sealed class SetupServerNetworkAccessTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("192.168.1.25", true)]
    [InlineData("203.0.113.25", false)]
    public async Task IsLocalNetworkRequest_AllowsLoopbackAndLanOnly(string remoteAddress, bool expected)
    {
        var networkManager = new Mock<INetworkManager>();
        networkManager
            .Setup(x => x.IsInLocalNetwork(It.IsAny<IPAddress>()))
            .Returns<IPAddress>(address => address.Equals(IPAddress.Parse("192.168.1.25")));

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);

        Assert.Equal(expected, SetupServer.IsLocalNetworkRequest(context, networkManager.Object));
        await Task.CompletedTask;
    }

    [Fact]
    public void IsLocalNetworkRequest_RejectsMissingNetworkManager()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        Assert.False(SetupServer.IsLocalNetworkRequest(context, null));
    }
}
