using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using MulletaFlix.Server.Health;
using Xunit;

namespace MulletaFlix.Server.Tests.Health;

public sealed class NebulaHealthCheckTests
{
    [Fact]
    public async Task DisabledNebula_IsHealthyWithoutCallingManager()
    {
        var manager = new Mock<INebulaFtpManager>(MockBehavior.Strict);
        var configuration = CreateConfiguration(enabled: false);
        var check = new NebulaHealthCheck(manager.Object, configuration.Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        manager.Verify(m => m.GetStatusAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnabledNebula_IsHealthyWhenStatusResponds()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaStatusDto { IsEnvioRunning = true });
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(true, result.Data["envioRunning"]);
    }

    [Fact]
    public async Task EnabledNebula_IsUnhealthyWhenNoServiceIsRunning()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaStatusDto());
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(false, result.Data["envioRunning"]);
        Assert.Equal(false, result.Data["downloaderRunning"]);
    }

    [Fact]
    public async Task EnabledNebula_IsUnhealthyWhenOnlyDownloaderIsRunning()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaStatusDto { IsDownloaderRunning = true });
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(true, result.Data["downloaderRunning"]);
    }

    [Fact]
    public async Task EnabledNebula_IsUnhealthyWhenStatusFails()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("status unavailable"));
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    private static Mock<IServerConfigurationManager> CreateConfiguration(bool enabled)
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.Setup(m => m.GetConfiguration("nebulaftp"))
            .Returns(new NebulaFtpConfiguration { Enabled = enabled });
        return configuration;
    }
}
