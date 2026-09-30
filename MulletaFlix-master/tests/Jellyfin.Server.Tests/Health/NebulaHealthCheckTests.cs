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
        manager.Verify(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnabledNebula_IsHealthyWhenStatusResponds()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaComponentHealthDto
            {
                MongoConnected = true,
                FtpListenerRunning = true,
                HttpListenerRunning = true,
                TelegramReady = true
            });
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(true, result.Data["mongoConnected"]);
        Assert.Equal(true, result.Data["ftpListenerRunning"]);
        Assert.Equal(true, result.Data["httpListenerRunning"]);
        Assert.Equal(true, result.Data["telegramReady"]);
        Assert.Equal(4, result.Data.Count);
        manager.Verify(m => m.GetStatusAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnabledNebula_IsHealthyWhenTelegramIsNotConfiguredButCoreComponentsAreReady()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaComponentHealthDto
            {
                MongoConnected = true,
                FtpListenerRunning = true,
                HttpListenerRunning = true,
                TelegramReady = false
            });
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(false, result.Data["telegramReady"]);
    }

    [Fact]
    public async Task EnabledNebula_IsUnhealthyWhenMongoIsUnavailable()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaComponentHealthDto
            {
                MongoConnected = false,
                FtpListenerRunning = true,
                HttpListenerRunning = true
            });
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(false, result.Data["mongoConnected"]);
    }

    [Fact]
    public async Task EnabledNebula_RecoversWhenMongoBecomesAvailableAgain()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.SetupSequence(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaComponentHealthDto
            {
                MongoConnected = false,
                FtpListenerRunning = true,
                HttpListenerRunning = true
            })
            .ReturnsAsync(new NebulaComponentHealthDto
            {
                MongoConnected = true,
                FtpListenerRunning = true,
                HttpListenerRunning = true
            });
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var unavailable = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
        var recovered = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, unavailable.Status);
        Assert.Equal(false, unavailable.Data["mongoConnected"]);
        Assert.Equal(HealthStatus.Healthy, recovered.Status);
        Assert.Equal(true, recovered.Data["mongoConnected"]);
        manager.Verify(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EnabledNebula_IsUnhealthyWhenListenersAreNotReady()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaComponentHealthDto
            {
                MongoConnected = true,
                FtpListenerRunning = true,
                HttpListenerRunning = false
            });
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(false, result.Data["httpListenerRunning"]);
    }

    [Fact]
    public async Task EnabledNebula_IsUnhealthyWhenFtpListenerIsNotReady()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaComponentHealthDto
            {
                MongoConnected = true,
                FtpListenerRunning = false,
                HttpListenerRunning = true
            });
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(false, result.Data["ftpListenerRunning"]);
    }

    [Fact]
    public async Task EnabledNebula_IsDegradedWhenTelegramIsNotReadyButStreamingWorks()
    {
        // Sem Telegram os uploads não acontecem, mas a mídia já enviada continua
        // sendo transmitida. Marcar isso como Unhealthy faria o `/ready` falhar e
        // um orquestrador retiraria o servidor de rotação apesar de a reprodução
        // estar funcionando; Degraded mantém o HTTP 200 do readiness e ainda
        // expõe a degradação.
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaComponentHealthDto
            {
                MongoConnected = true,
                FtpListenerRunning = true,
                HttpListenerRunning = true,
                TelegramReady = false
            });
        var check = new NebulaHealthCheck(
            manager.Object,
            CreateConfiguration(enabled: true, apiHash: new string('a', 32)).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(false, result.Data["telegramReady"]);
        Assert.Equal(true, result.Data["mongoConnected"]);
    }

    [Fact]
    public async Task EnabledNebula_PrefersUnhealthyOverDegradedWhenCoreDependencyIsDown()
    {
        // Uma dependência essencial em falha não pode ser rebaixada a Degraded
        // só porque o Telegram também está fora.
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NebulaComponentHealthDto
            {
                MongoConnected = false,
                FtpListenerRunning = true,
                HttpListenerRunning = true,
                TelegramReady = false
            });
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task EnabledNebula_FailureDescriptionDoesNotExposeExceptionDetail()
    {
        // O `/health` exige acesso local ou elevação, mas a descrição não deve
        // carregar mensagem de exceção: ela pode conter caminho, host ou
        // fragmento de configuração.
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mongodb://user:SuperSecret@host/db failed at C:/media/Sensitive"));
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Description);
        Assert.DoesNotContain("SuperSecret", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Sensitive", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb://", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnabledNebula_IsUnhealthyWhenStatusFails()
    {
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetComponentHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("status unavailable"));
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public async Task EnabledNebula_PropagatesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.GetComponentHealthAsync(cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var check = new NebulaHealthCheck(manager.Object, CreateConfiguration(enabled: true).Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => check.CheckHealthAsync(new HealthCheckContext(), cancellation.Token));
    }

    private static Mock<IServerConfigurationManager> CreateConfiguration(bool enabled, string apiHash = "")
    {
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.Setup(m => m.GetConfiguration("nebulaftp"))
            .Returns(new NebulaFtpConfiguration { Enabled = enabled, ApiHash = apiHash });
        return configuration;
    }
}
