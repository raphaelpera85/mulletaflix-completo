using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Cobre a reação do serviço à configuração do Nebula.
/// </summary>
/// <remarks>
/// O serviço só avaliava `Enabled` uma vez, no `ApplicationStarted`. Com o
/// Nebula desabilitado naquele instante, o `return` encerrava a inicialização em
/// definitivo: habilitar depois pela interface salvava a configuração mas não
/// iniciava nada, e o usuário ficava preso em "aguardando início do servidor"
/// mesmo com o servidor já inicializado. A única saída era reiniciar o processo.
/// </remarks>
public sealed class NebulaHostedServiceConfigurationTests
{
    private static NebulaHostedService CreateService(
        Mock<INebulaFtpManager> manager,
        Mock<IServerConfigurationManager> configManager,
        IHostApplicationLifetime lifetime)
        => CreateService(manager, configManager, lifetime, Task.Delay);

    private static NebulaHostedService CreateService(
        Mock<INebulaFtpManager> manager,
        Mock<IServerConfigurationManager> configManager,
        IHostApplicationLifetime lifetime,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
        => new(
            manager.Object,
            configManager.Object,
            lifetime,
            Mock.Of<ILibraryManager>(),
            NullLogger<NebulaHostedService>.Instance,
            delayAsync);

    private static NebulaHostedService CreateService(
        Mock<INebulaFtpManager> manager,
        Mock<IServerConfigurationManager> configManager,
        IHostApplicationLifetime lifetime,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        Func<TimeSpan, CancellationToken, Task> runtimeDelayAsync)
        => new(
            manager.Object,
            configManager.Object,
            lifetime,
            Mock.Of<ILibraryManager>(),
            NullLogger<NebulaHostedService>.Instance,
            delayAsync,
            runtimeDelayAsync);

    private static Mock<IServerConfigurationManager> CreateConfigManager(NebulaFtpConfiguration config)
    {
        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(m => m.GetConfiguration("nebulaftp")).Returns(config);
        return configManager;
    }

    [Fact]
    public async Task EnablingNebulaAfterStartup_StartsTheServiceWithoutRestartingTheServer()
    {
        var config = new NebulaFtpConfiguration { Enabled = false, UseMappedDrive = false };
        var configManager = CreateConfigManager(config);

        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        using var lifetime = new TestLifetime();
        var service = CreateService(manager, configManager, lifetime);
        await service.StartAsync(CancellationToken.None);

        // Servidor termina o startup com o Nebula desabilitado.
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        manager.Verify(m => m.StartEnvioAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        // O administrador habilita pela interface: a configuração é salva e o
        // evento de atualização é disparado.
        config.Enabled = true;
        configManager.Raise(
            m => m.NamedConfigurationUpdated += null,
            new ConfigurationUpdateEventArgs("nebulaftp", config));
        await service.WaitForPendingWorkAsync();

        // Sem reiniciar o processo, o Nebula precisa subir.
        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfigurationUpdateForAnotherSection_IsIgnored()
    {
        var config = new NebulaFtpConfiguration { Enabled = false };
        var configManager = CreateConfigManager(config);
        var manager = new Mock<INebulaFtpManager>();

        using var lifetime = new TestLifetime();
        var service = CreateService(manager, configManager, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();

        config.Enabled = true;
        configManager.Raise(
            m => m.NamedConfigurationUpdated += null,
            new ConfigurationUpdateEventArgs("encoding", new object()));
        await service.WaitForPendingWorkAsync();

        manager.Verify(m => m.StartEnvioAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepeatedEnabledUpdates_DoNotStartTheServiceTwice()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configManager = CreateConfigManager(config);

        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        using var lifetime = new TestLifetime();
        var service = CreateService(manager, configManager, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();

        // Salvar a configuração de novo, ainda habilitada, não pode reiniciar o
        // pipeline: cada salvamento na tela dispararia um start concorrente.
        for (var i = 0; i < 3; i++)
        {
            configManager.Raise(
                m => m.NamedConfigurationUpdated += null,
                new ConfigurationUpdateEventArgs("nebulaftp", config));
            await service.WaitForPendingWorkAsync();
        }

        manager.Verify(m => m.StartEnvioAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DisablingNebulaAfterItStarted_StopsTheService()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configManager = CreateConfigManager(config);

        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        using var lifetime = new TestLifetime();
        var service = CreateService(manager, configManager, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        manager.Verify(m => m.StartEnvioAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);

        config.Enabled = false;
        configManager.Raise(
            m => m.NamedConfigurationUpdated += null,
            new ConfigurationUpdateEventArgs("nebulaftp", config));
        await service.WaitForPendingWorkAsync();

        manager.Verify(m => m.StopEnvioAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FailedDownloader_CanRecoverOnConfigurationUpdateWithoutRestartingEnvio()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.SetupSequence(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Downloader configuration unavailable"))
            .ReturnsAsync(true);
        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();

        configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
        await service.WaitForPendingWorkAsync();

        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloaderExitAfterStartup_IsDetectedAndRestartedWithoutRestartingEnvio()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var downloaderAlive = false;
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            downloaderAlive = true;
            return Task.FromResult(true);
        });
        manager.SetupGet(m => m.IsDownloaderRunning).Returns(() => downloaderAlive);
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();

        await service.CheckDownloaderRuntimeAsync();
        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);

        downloaderAlive = false;
        await service.CheckDownloaderRuntimeAsync();

        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(downloaderAlive);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DownloaderRuntimeCheck_WhenNebulaDisabled_DoesNotRestartWorker()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();

        config.Enabled = false;
        await service.CheckDownloaderRuntimeAsync();

        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DownloaderRuntimeCheck_WhenShutdownIsPending_DoesNotRestartWorker()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var downloaderAlive = false;
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            downloaderAlive = true;
            return Task.FromResult(true);
        });
        manager.SetupGet(m => m.IsDownloaderRunning).Returns(() => downloaderAlive);
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();

        config.Enabled = false;
        configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
        await service.WaitForPendingWorkAsync();

        config.Enabled = true;
        downloaderAlive = false;
        await service.CheckDownloaderRuntimeAsync();

        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.StopEnvioAsync(It.IsAny<CancellationToken>()), Times.Once);
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RuntimeMonitor_DetectsExitAndRetriesUntilDownloaderRecovers()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var monitorTick = new SemaphoreSlim(0);
        var monitorWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var retriesExhausted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloaderAlive = false;
        var startAttempts = 0;
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            startAttempts++;
            if (startAttempts == 1 || startAttempts == 5)
            {
                downloaderAlive = true;
                if (startAttempts == 5)
                {
                    recovered.TrySetResult();
                }

                return Task.FromResult(true);
            }

            if (startAttempts == 4)
            {
                retriesExhausted.TrySetResult();
            }

            return Task.FromResult(false);
        });
        manager.SetupGet(m => m.IsDownloaderRunning).Returns(() => downloaderAlive);
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var runtimeDelayCalls = 0;
        Task RuntimeDelayAsync(TimeSpan _, CancellationToken token)
        {
            if (Interlocked.Increment(ref runtimeDelayCalls) == 1)
            {
                monitorWaiting.TrySetResult();
            }

            return monitorTick.WaitAsync(token);
        }

        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime, (_, _) => Task.CompletedTask, RuntimeDelayAsync);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        await monitorWaiting.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        downloaderAlive = false;
        monitorTick.Release();
        await retriesExhausted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(4, startAttempts);
        await monitorWaiting.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        monitorTick.Release();
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(5, startAttempts);
        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        await service.StopAsync(CancellationToken.None);
        monitorTick.Dispose();
    }

    [Fact]
    public async Task Stop_WaitsForRuntimeMonitorRestartBeforeStoppingNebulaResources()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var monitorTick = new SemaphoreSlim(0);
        var monitorWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restartEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restartCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var downloaderAlive = false;
        var startAttempts = 0;
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            startAttempts++;
            if (startAttempts == 1)
            {
                downloaderAlive = true;
                return Task.FromResult(true);
            }

            restartEntered.TrySetResult();
            return restartCompletion.Task;
        });
        manager.SetupGet(m => m.IsDownloaderRunning).Returns(() => downloaderAlive);
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var runtimeDelayCalls = 0;
        Task RuntimeDelayAsync(TimeSpan _, CancellationToken token)
        {
            if (Interlocked.Increment(ref runtimeDelayCalls) == 1)
            {
                monitorWaiting.TrySetResult();
            }

            return monitorTick.WaitAsync(token);
        }

        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime, (_, _) => Task.CompletedTask, RuntimeDelayAsync);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        await monitorWaiting.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        downloaderAlive = false;
        monitorTick.Release();
        await restartEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var stopTask = service.StopAsync(CancellationToken.None);
        Assert.False(stopTask.IsCompleted);
        manager.Verify(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>()), Times.Never);
        manager.Verify(m => m.StopEnvioAsync(It.IsAny<CancellationToken>()), Times.Never);

        restartCompletion.TrySetResult(true);
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        manager.Verify(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.StopEnvioAsync(It.IsAny<CancellationToken>()), Times.Once);
        monitorTick.Dispose();
    }

    [Fact]
    public async Task EnablingMappedDriveAfterStartup_MountsWithoutRestartingHealthyComponents()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.MountDriveNAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var library = new Mock<ILibraryManager>();
        library.Setup(m => m.ValidateMediaLibrary(It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var lifetime = new TestLifetime();
        using var service = new NebulaHostedService(manager.Object, configuration.Object, lifetime, library.Object, NullLogger<NebulaHostedService>.Instance);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();

        config.UseMappedDrive = true;
        configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
        await service.WaitForPendingWorkAsync();
        configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
        await service.WaitForPendingWorkAsync();

        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.MountDriveNAsync(It.IsAny<CancellationToken>()), Times.Once);
        library.Verify(m => m.ValidateMediaLibrary(It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TransientStartupTimeout_IsRetriedBeforeStartingDownloader()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var manager = new Mock<INebulaFtpManager>();
        manager.SetupSequence(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Transient startup timeout"))
            .ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var retryDelays = new List<TimeSpan>();
        Task RecordRetryDelayAsync(TimeSpan delay, CancellationToken _)
        {
            retryDelays.Add(delay);
            return Task.CompletedTask;
        }

        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, CreateConfigManager(config), lifetime, RecordRetryDelayAsync);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Equal([TimeSpan.FromSeconds(5)], retryDelays);
        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnvioStartupException_DoesNotPreventDownloaderFromStarting()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FTP port is already in use"));
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();

        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.MountDriveNAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnvioFailure_DoesNotPreventIndependentDownloaderStartup_AndSkipsDriveMount()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = true };
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var retryDelays = new List<TimeSpan>();
        Task RecordRetryDelayAsync(TimeSpan delay, CancellationToken _)
        {
            retryDelays.Add(delay);
            return Task.CompletedTask;
        }

        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, CreateConfigManager(config), lifetime, RecordRetryDelayAsync);

        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();

        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.Equal([TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)], retryDelays);
        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.MountDriveNAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InternalUnchangedConfigurationSave_DoesNotRescheduleFailedPipeline()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.SetupSequence(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
                return Task.FromException<bool>(new InvalidOperationException("Invalid monitor configuration"));
            })
            .ReturnsAsync(true);
        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        await service.WaitForPendingWorkAsync();
        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangedConfigurationDuringStartup_IsAppliedAfterTheCurrentAttempt()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).Returns(() =>
        {
            config.Enabled = false;
            configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
            return Task.FromResult(true);
        });
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        await service.WaitForPendingWorkAsync();
        manager.Verify(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.StopEnvioAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellationDuringStartup_DoesNotRetryOrStartDependentComponents()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = true };
        var manager = new Mock<INebulaFtpManager>();
        using var lifetime = new TestLifetime();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).Returns(() =>
        {
            lifetime.StopApplication();
            return Task.FromCanceled<bool>(lifetime.ApplicationStopping);
        });
        using var service = CreateService(manager, CreateConfigManager(config), lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Never);
        manager.Verify(m => m.MountDriveNAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StoppingBeforeApplicationStarted_PreventsLateStartup()
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, CreateConfigManager(config), lifetime);
        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        manager.Verify(m => m.StartEnvioAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Stop_CancelsAndWaitsForConfigurationStartupBeforeStoppingResources()
    {
        var config = new NebulaFtpConfiguration { Enabled = false, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var manager = new Mock<INebulaFtpManager>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starting = false;
        var stoppedDuringStartup = false;
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).Returns(async (bool _, CancellationToken token) =>
        {
            starting = true;
            entered.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token);
                return true;
            }
            finally
            {
                starting = false;
            }
        });
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>())).Callback(() => stoppedDuringStartup = starting).ReturnsAsync(true);
        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        config.Enabled = true;
        configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        try
        {
            await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(stoppedDuringStartup);
            Assert.False(starting);
            manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            lifetime.StopApplication();
            await service.WaitForPendingWorkAsync();
        }
    }

    [Fact]
    public async Task StopTimeout_DoesNotDisposeResourcesWhileStartupIgnoresCancellation()
    {
        var config = new NebulaFtpConfiguration { Enabled = false, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var manager = new Mock<INebulaFtpManager>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var startup = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).Returns(() =>
        {
            entered.SetResult();
            return startup.Task;
        });
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        config.Enabled = true;
        configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StopAsync(deadline.Token));
            manager.Verify(m => m.StopEnvioAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        finally
        {
            lifetime.StopApplication();
            startup.TrySetResult(true);
            await service.WaitForPendingWorkAsync();
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedStop_IsRetriedBeforeStartingThePipelineAgain(bool throws)
    {
        var config = new NebulaFtpConfiguration { Enabled = true, UseMappedDrive = false };
        var configuration = CreateConfigManager(config);
        var manager = new Mock<INebulaFtpManager>();
        manager.Setup(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        manager.Setup(m => m.StopDownloaderAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var stopFails = true;
        manager.Setup(m => m.StopEnvioAsync(It.IsAny<CancellationToken>())).Returns(() =>
            stopFails && throws ? Task.FromException<bool>(new InvalidOperationException("stop failed")) : Task.FromResult(!stopFails));
        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, configuration, lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();

        config.Enabled = false;
        configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
        await service.WaitForPendingWorkAsync();
        config.Enabled = true;
        configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
        await service.WaitForPendingWorkAsync();
        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        manager.Verify(m => m.StopEnvioAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        stopFails = false;
        configuration.Raise(m => m.NamedConfigurationUpdated += null, new ConfigurationUpdateEventArgs("nebulaftp", config));
        await service.WaitForPendingWorkAsync();
        manager.Verify(m => m.StopEnvioAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Exactly(2));
        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        await service.StopAsync(CancellationToken.None);
    }

    private sealed class TestLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;

        public CancellationToken ApplicationStopping => _stopping.Token;

        public CancellationToken ApplicationStopped => _stopped.Token;

        public void TriggerStarted() => _started.Cancel();

        public void StopApplication() => _stopping.Cancel();

        public void Dispose()
        {
            StopApplication();
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }
}
