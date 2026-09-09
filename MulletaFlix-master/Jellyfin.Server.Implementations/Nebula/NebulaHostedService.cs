#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Serviço de ciclo de vida que inicializa o Nebula (Modo Envio e Disco N) junto com o MulletaFlix Server.
/// </summary>
public sealed class NebulaHostedService : IHostedService
{
    private readonly INebulaFtpManager _nebulaManager;
    private readonly IServerConfigurationManager _configManager;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<NebulaHostedService> _logger;
    private Task? _startupTask;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaHostedService"/>.
    /// </summary>
    public NebulaHostedService(
        INebulaFtpManager nebulaManager,
        IServerConfigurationManager configManager,
        IHostApplicationLifetime lifetime,
        ILogger<NebulaHostedService> logger)
    {
        _nebulaManager = nebulaManager;
        _configManager = configManager;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStarted.Register(OnApplicationStarted);
        return Task.CompletedTask;
    }

    private void OnApplicationStarted()
    {
        var startupCancellationToken = _lifetime.ApplicationStopping;
        _startupTask = Task.Run(
            async () =>
            {
                try
                {
                    var config = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
                    if (config == null || !config.Enabled)
                    {
                        _logger.LogInformation("[NEBULA-STARTUP] NebulaFTP está desabilitado na configuração.");
                        return;
                    }

                    _logger.LogInformation("[NEBULA-STARTUP] Servidor MulletaFlix inicializado com sucesso. Iniciando modo Envio do Nebula...");

                    var envioStarted = await _nebulaManager.StartEnvioAsync(streamOnly: false, startupCancellationToken).ConfigureAwait(false);
                    if (envioStarted)
                    {
                        _logger.LogInformation("[NEBULA-STARTUP] Modo Envio iniciado com sucesso.");
                        if (config.UseMappedDrive)
                        {
                            _logger.LogInformation("[NEBULA-STARTUP] UseMappedDrive=true: montando disco N: em sequência...");
                            await _nebulaManager.MountDriveNAsync(startupCancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            _logger.LogInformation("[NEBULA-STARTUP] UseMappedDrive=false: pulando montagem da unidade N:. Streaming via HTTP/FTP direto.");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[NEBULA-STARTUP] Falha ao iniciar modo Envio do Nebula.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[NEBULA-STARTUP] Erro durante a inicialização automática sequencial do Nebula / Disco N.");
                }
            },
            CancellationToken.None);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[NEBULA-SHUTDOWN] Encerrando serviços do NebulaFTP, Downloader... (unidade N: será desmontada apenas se UseMappedDrive=true)");

        if (_startupTask != null)
        {
            try
            {
                await _startupTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("[NEBULA-SHUTDOWN] Inicialização automática cancelada pelo timeout de encerramento.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NEBULA-SHUTDOWN] Falha ao aguardar a inicialização automática do Nebula.");
            }

            _startupTask = null;
        }

        try
        {
            await _nebulaManager.StopDownloaderAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-SHUTDOWN] Erro ao parar o Downloader do Nebula.");
        }

        try
        {
            await _nebulaManager.StopEnvioAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-SHUTDOWN] Erro ao parar o Envio do Nebula.");
        }
    }
}
