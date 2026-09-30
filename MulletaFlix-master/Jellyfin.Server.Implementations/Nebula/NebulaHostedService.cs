#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Serviço de ciclo de vida que inicializa o Nebula (Modo Envio e Disco N) junto com o MulletaFlix Server.
/// </summary>
public sealed class NebulaHostedService : IHostedService, IDisposable
{
    private readonly INebulaFtpManager _nebulaManager;
    private readonly IServerConfigurationManager _configManager;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<NebulaHostedService> _logger;

    // Serializa inicialização/parada disparadas pelo startup e por mudanças de
    // configuração, que podem chegar concorrentemente.
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private Task? _startupTask;
    private Task _pendingWork = Task.CompletedTask;
    private bool _running;
    private bool _applicationStarted;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaHostedService"/>.
    /// </summary>
    public NebulaHostedService(
        INebulaFtpManager nebulaManager,
        IServerConfigurationManager configManager,
        IHostApplicationLifetime lifetime,
        ILibraryManager libraryManager,
        ILogger<NebulaHostedService> logger)
    {
        _nebulaManager = nebulaManager;
        _configManager = configManager;
        _lifetime = lifetime;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _lifetime.ApplicationStarted.Register(OnApplicationStarted);

        // Sem este listener, `Enabled` era avaliado uma única vez no
        // `ApplicationStarted`. Com o Nebula desabilitado naquele instante, a
        // inicialização encerrava em definitivo e habilitar pela interface não
        // iniciava nada — a tela ficava em "aguardando início do servidor" com o
        // servidor já inicializado, e a única saída era reiniciar o processo.
        _configManager.NamedConfigurationUpdated += OnNamedConfigurationUpdated;
        return Task.CompletedTask;
    }

    private void OnNamedConfigurationUpdated(object? sender, ConfigurationUpdateEventArgs e)
    {
        if (!string.Equals(e.Key, "nebulaftp", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Antes do `ApplicationStarted` o fluxo normal de startup ainda vai
        // rodar; agir aqui duplicaria a inicialização.
        if (!Volatile.Read(ref _applicationStarted))
        {
            return;
        }

        QueueWork(ApplyConfigurationAsync);
    }

    private async Task ApplyConfigurationAsync()
    {
        var config = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        var shouldRun = config?.Enabled == true;

        await _transitionLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (shouldRun == _running)
            {
                // Salvar a mesma tela repetidamente não pode reiniciar o
                // pipeline nem disparar starts concorrentes.
                return;
            }

            if (shouldRun)
            {
                _logger.LogInformation("[NEBULA-CONFIG] Nebula habilitado na configuração. Iniciando sem reiniciar o servidor...");
                await StartPipelineAsync(config!, _lifetime.ApplicationStopping).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation("[NEBULA-CONFIG] Nebula desabilitado na configuração. Encerrando os serviços em execução...");
                await StopPipelineAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-CONFIG] Falha ao aplicar a mudança de configuração do Nebula.");
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    private void QueueWork(Func<Task> work)
    {
        lock (_transitionLock)
        {
            var previous = _pendingWork;
            _pendingWork = Task.Run(async () =>
            {
                try
                {
                    await previous.ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Falha anterior já foi registrada; não bloqueia a próxima transição.
                }

                await work().ConfigureAwait(false);
            });
        }
    }

    /// <summary>
    /// Aguarda as transições pendentes. Destinado a testes determinísticos.
    /// </summary>
    /// <returns>Tarefa concluída quando não há transição em andamento.</returns>
    internal async Task WaitForPendingWorkAsync()
    {
        Task pending;
        lock (_transitionLock)
        {
            pending = _pendingWork;
        }

        try
        {
            await pending.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // O objetivo é apenas sincronizar; a falha já foi registrada.
        }

        if (_startupTask is not null)
        {
            try
            {
                await _startupTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // idem.
            }
        }
    }

    private void OnApplicationStarted()
    {
        Volatile.Write(ref _applicationStarted, true);
        var startupCancellationToken = _lifetime.ApplicationStopping;
        _startupTask = Task.Run(
            async () =>
            {
                await _transitionLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    var config = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
                    if (config == null || !config.Enabled)
                    {
                        _logger.LogInformation(
                            "[NEBULA-STARTUP] NebulaFTP está desabilitado na configuração. Habilitar pela interface inicia o serviço sem reiniciar o servidor.");
                        return;
                    }

                    await StartPipelineAsync(config, startupCancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[NEBULA-STARTUP] Erro durante a inicialização automática sequencial do Nebula / Disco N.");
                }
                finally
                {
                    _transitionLock.Release();
                }
            },
            CancellationToken.None);
    }

    private async Task StartPipelineAsync(NebulaFtpConfiguration config, CancellationToken startupCancellationToken)
    {
        _logger.LogInformation("[NEBULA-STARTUP] Servidor MulletaFlix inicializado. Iniciando Envio, Downloader e montagem do disco N em sequência...");

        var envioStarted = await StartWithRetryAsync(
            () => _nebulaManager.StartEnvioAsync(streamOnly: false, startupCancellationToken),
            "Envio",
            startupCancellationToken).ConfigureAwait(false);
        if (!envioStarted)
        {
            _logger.LogWarning("[NEBULA-STARTUP] Falha ao iniciar modo Envio do Nebula.");
            return;
        }

        _running = true;
        _logger.LogInformation("[NEBULA-STARTUP] Modo Envio iniciado com sucesso.");

        // Start the downloader in the same server lifecycle. The
        // manager reuses the Mongo/Telegram runtime created by
        // StartEnvioAsync and its own lock prevents duplicates.
        var downloaderStarted = await StartWithRetryAsync(
            () => _nebulaManager.StartDownloaderAsync(startupCancellationToken),
            "Downloader",
            startupCancellationToken).ConfigureAwait(false);
        if (downloaderStarted)
        {
            _logger.LogInformation("[NEBULA-STARTUP] Downloader STRM iniciado com sucesso.");
        }
        else
        {
            _logger.LogWarning("[NEBULA-STARTUP] Downloader STRM não foi iniciado. Verifique as pastas de monitoramento configuradas.");
        }

        if (!config.UseMappedDrive)
        {
            _logger.LogInformation("[NEBULA-STARTUP] UseMappedDrive=false: pulando montagem da unidade N:. Streaming via HTTP/FTP direto.");
            return;
        }

        _logger.LogInformation("[NEBULA-STARTUP] UseMappedDrive=true: montando disco N: em sequência...");
        var mounted = await StartWithRetryAsync(
            () => _nebulaManager.MountDriveNAsync(startupCancellationToken),
            "Disco N",
            startupCancellationToken).ConfigureAwait(false);
        if (mounted)
        {
            _logger.LogInformation("[NEBULA-STARTUP] Disco N: montado e acessível.");
            _logger.LogInformation("[NEBULA-STARTUP] Unidade N disponível. Iniciando refresh da biblioteca para indexar filmes, séries e capas...");
            await _libraryManager.ValidateMediaLibrary(new Progress<double>(), startupCancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogWarning("[NEBULA-STARTUP] Disco N: não foi montado. O envio e o Downloader continuam ativos.");
        }
    }

    private async Task StopPipelineAsync(CancellationToken cancellationToken)
    {
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

        _running = false;
    }

    private async Task<bool> StartWithRetryAsync(
        Func<Task<bool>> startAction,
        string componentName,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await startAction().ConfigureAwait(false))
            {
                return true;
            }

            if (attempt < maxAttempts)
            {
                var delaySeconds = attempt * 5;
                _logger.LogWarning(
                    "[NEBULA-STARTUP] {Component} não iniciou na tentativa {Attempt}/{MaxAttempts}. Nova tentativa em {DelaySeconds}s.",
                    componentName,
                    attempt,
                    maxAttempts,
                    delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[NEBULA-SHUTDOWN] Encerrando serviços do NebulaFTP, Downloader... (unidade N: será desmontada apenas se UseMappedDrive=true)");

        _configManager.NamedConfigurationUpdated -= OnNamedConfigurationUpdated;

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

        await StopPipelineAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _configManager.NamedConfigurationUpdated -= OnNamedConfigurationUpdated;
        _transitionLock.Dispose();
    }
}
