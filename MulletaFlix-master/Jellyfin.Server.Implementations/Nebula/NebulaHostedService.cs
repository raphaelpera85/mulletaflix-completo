#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.IO;
using System.Text.Json;
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
    private readonly CancellationTokenSource _shutdownCancellation;
    private CancellationTokenRegistration _startupRegistration;

    // Serializa inicialização/parada disparadas pelo startup e por mudanças de
    // configuração, que podem chegar concorrentemente.
    private readonly SemaphoreSlim _transitionLock = new(1, 1);
    private Task? _startupTask;
    private Task _pendingWork = Task.CompletedTask;
    private bool _running;
    private bool _downloaderRunning;
    private bool _driveMounted;
    private bool _stopPending;
    private string? _startupConfigurationSnapshot;
    private bool _applicationStarted;
    private bool _stopping;
    private int _disposed;

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
        _shutdownCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _startupRegistration = _lifetime.ApplicationStarted.Register(OnApplicationStarted);

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
        if (!Volatile.Read(ref _applicationStarted) || Volatile.Read(ref _stopping))
        {
            return;
        }

        // Manager startup persists configuration too. An unchanged internal save
        // must not enqueue an unbounded series of retries after partial failure.
        // Changed settings still queue a transition; identical saves after the
        // attempt ends remain an explicit retry opportunity for the operator.
        var startupSnapshot = Volatile.Read(ref _startupConfigurationSnapshot);
        if (startupSnapshot != null && e.NewConfiguration is NebulaFtpConfiguration updated
            && string.Equals(startupSnapshot, JsonSerializer.Serialize(updated), StringComparison.Ordinal))
        {
            return;
        }

        QueueWork(ApplyConfigurationAsync);
    }

    private async Task ApplyConfigurationAsync()
    {
        await _transitionLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (Volatile.Read(ref _stopping))
            {
                return;
            }

            // Never restart over resources whose previous shutdown failed.
            if (_stopPending)
            {
                await StopPipelineAsync(_shutdownCancellation.Token).ConfigureAwait(false);
            }

            var config = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
            var shouldRun = config?.Enabled == true;
            var pipelineReady = _running && _downloaderRunning && (config?.UseMappedDrive != true || _driveMounted);
            if (shouldRun ? pipelineReady : !_running)
            {
                // Salvar a mesma tela repetidamente não pode reiniciar o
                // pipeline nem disparar starts concorrentes.
                return;
            }

            if (shouldRun)
            {
                _logger.LogInformation("[NEBULA-CONFIG] Nebula habilitado na configuração. Iniciando sem reiniciar o servidor...");
                await StartPipelineAsync(config!, _shutdownCancellation.Token).ConfigureAwait(false);
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
            if (Volatile.Read(ref _stopping))
            {
                return;
            }

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
        if (Volatile.Read(ref _stopping))
        {
            return;
        }

        Volatile.Write(ref _applicationStarted, true);
        // Start STRM indexing independently of whether the Nebula transfer pipeline is enabled.
        // The manager serves the current snapshot immediately and scans configured libraries in the background.
        _nebulaManager.GetMediaSuggestionCatalog();
        var startupCancellationToken = _shutdownCancellation.Token;
        _startupTask = Task.Run(
            async () =>
            {
                await _transitionLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    if (Volatile.Read(ref _stopping))
                    {
                        return;
                    }

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
        Volatile.Write(ref _startupConfigurationSnapshot, JsonSerializer.Serialize(config));
        try
        {
            await StartComponentsAsync(config, startupCancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Volatile.Write(ref _startupConfigurationSnapshot, null);
        }
    }

    private async Task StartComponentsAsync(NebulaFtpConfiguration config, CancellationToken startupCancellationToken)
    {
        _logger.LogInformation("[NEBULA-STARTUP] Servidor MulletaFlix inicializado. Iniciando Envio, Downloader e montagem do disco N em sequência...");

        if (!_running)
        {
            _running = await StartWithRetryAsync(
                () => _nebulaManager.StartEnvioAsync(streamOnly: false, startupCancellationToken),
                "Envio",
                startupCancellationToken).ConfigureAwait(false);
            if (!_running)
            {
                _logger.LogWarning("[NEBULA-STARTUP] Falha ao iniciar modo Envio do Nebula.");
                return;
            }

            _logger.LogInformation("[NEBULA-STARTUP] Modo Envio iniciado com sucesso.");
        }

        // Start the downloader in the same server lifecycle. The
        // manager reuses the Mongo/Telegram runtime created by
        // StartEnvioAsync and its own lock prevents duplicates.
        if (!_downloaderRunning)
        {
            _downloaderRunning = await StartWithRetryAsync(
                () => _nebulaManager.StartDownloaderAsync(startupCancellationToken),
                "Downloader",
                startupCancellationToken).ConfigureAwait(false);
        }

        if (_downloaderRunning)
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

        if (_driveMounted)
        {
            return;
        }

        _logger.LogInformation("[NEBULA-STARTUP] UseMappedDrive=true: montando disco N: em sequência...");
        _driveMounted = await StartWithRetryAsync(
            () => _nebulaManager.MountDriveNAsync(startupCancellationToken),
            "Disco N",
            startupCancellationToken).ConfigureAwait(false);
        if (_driveMounted)
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
        _stopPending = true;
        var downloaderStopped = false;
        var envioStopped = false;
        try
        {
            downloaderStopped = await _nebulaManager.StopDownloaderAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-SHUTDOWN] Erro ao parar o Downloader do Nebula.");
        }

        try
        {
            envioStopped = await _nebulaManager.StopEnvioAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-SHUTDOWN] Erro ao parar o Envio do Nebula.");
        }

        if (envioStopped)
        {
            _running = false;
        }

        if (downloaderStopped)
        {
            _downloaderRunning = false;
        }

        if (!envioStopped || !downloaderStopped)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Nebula shutdown incomplete. Pending resources must be stopped before restarting.");
        }

        _driveMounted = false;
        _stopPending = false;
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
            try
            {
                if (await startAction().ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is TimeoutException or IOException)
            {
                _logger.LogWarning(ex, "[NEBULA-STARTUP] Falha transitória em {Component}, tentativa {Attempt}/{MaxAttempts}.", componentName, attempt, maxAttempts);
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

        Volatile.Write(ref _stopping, true);
        _startupRegistration.Dispose();
        _configManager.NamedConfigurationUpdated -= OnNamedConfigurationUpdated;
        _shutdownCancellation.Cancel();

        // Configuration-triggered startup owns the same resources as cold startup.
        // Cancellation plus draining prevents shutdown racing either transition.
        await GetPendingTransitions().WaitAsync(cancellationToken).ConfigureAwait(false);
        await _transitionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopPipelineAsync(cancellationToken).ConfigureAwait(false);
            _startupTask = null;
        }
        finally
        {
            _transitionLock.Release();
        }
    }

    private Task GetPendingTransitions()
    {
        lock (_transitionLock)
        {
            return Task.WhenAll(_startupTask ?? Task.CompletedTask, _pendingWork);
        }
    }

    private void DisposeSynchronizationResources()
    {
        _shutdownCancellation.Dispose();
        _transitionLock.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Volatile.Write(ref _stopping, true);
        _startupRegistration.Dispose();
        _configManager.NamedConfigurationUpdated -= OnNamedConfigurationUpdated;
        _shutdownCancellation.Cancel();
        var pending = GetPendingTransitions();
        if (pending.IsCompleted)
        {
            DisposeSynchronizationResources();
        }
        else
        {
            // A host timeout can leave an uncooperative startup running. Its
            // finally block must still be able to release the transition lock.
            _ = pending.ContinueWith(
                _ => DisposeSynchronizationResources(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
