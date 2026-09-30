using System;
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
        => new(
            manager.Object,
            configManager.Object,
            lifetime,
            Mock.Of<ILibraryManager>(),
            NullLogger<NebulaHostedService>.Instance);

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
        using var lifetime = new TestLifetime();
        using var service = CreateService(manager, CreateConfigManager(config), lifetime);
        await service.StartAsync(CancellationToken.None);
        lifetime.TriggerStarted();
        await service.WaitForPendingWorkAsync();
        manager.Verify(m => m.StartEnvioAsync(false, It.IsAny<CancellationToken>()), Times.Exactly(2));
        manager.Verify(m => m.StartDownloaderAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            await service.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
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
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
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
            _started.Dispose();
            _stopping.Dispose();
            _stopped.Dispose();
        }
    }
}
