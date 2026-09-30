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
