#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FubarDev.FtpServer;
using FubarDev.FtpServer.AccountManagement;
using FubarDev.FtpServer.Commands;
using FubarDev.FtpServer.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Hospeda e executa o servidor FTP nativo em C# no MulletaFlix.
/// </summary>
public sealed class NebulaFtpServerHost : IAsyncDisposable, IDisposable
{
    private readonly ILogger<NebulaFtpServerHost> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private IFtpServerHost? _ftpServerHost;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaFtpServerHost"/>.
    /// </summary>
    public NebulaFtpServerHost(
            NebulaMongoContext mongoContext,
            NebulaTelegramPool telegramPool,
            NebulaUploadEngine? uploadEngine,
            ILogger<NebulaFtpServerHost> logger,
            ILogger<NebulaFileSystem> fsLogger,
            ILogger<NebulaFtpMembershipProvider> authLogger,
            string serverAddress = "127.0.0.1",
            int ftpPort = 2121,
            int httpPort = 2123,
            int maxActiveConnections = 32)
        {
            _logger = logger;

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(mongoContext);
            services.AddSingleton(telegramPool);
            if (uploadEngine != null)
            {
                services.AddSingleton(uploadEngine);
            }
            services.AddSingleton(fsLogger);
            services.AddSingleton(authLogger);

            // Registra serviços do FTP FubarDev
            services.AddFtpServer(_ => { });

            services.Configure<FtpServerOptions>(opt =>
            {
                opt.ServerAddress = string.IsNullOrWhiteSpace(serverAddress) ? "127.0.0.1" : serverAddress;
                opt.Port = ftpPort > 0 ? ftpPort : 2121;
                opt.MaxActiveConnections = Math.Max(256, maxActiveConnections);
            });

            services.Configure<FtpConnectionOptions>(opt =>
            {
                opt.InactivityTimeout = TimeSpan.FromHours(2);
            });

// Substitui pelo FileSystem e Membership customizados do Nebula
            services.AddSingleton<IFileSystemClassFactory, NebulaFileSystemProvider>();
            services.AddSingleton<IMembershipProvider, NebulaFtpMembershipProvider>();

            _serviceProvider = services.BuildServiceProvider();
        }

    /// <summary>
    /// Obtém se o servidor FTP nativo está ativo.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Inicia a escuta do servidor FTP nativo.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                _logger.LogInformation("[NEBULA-FTP-SERVER] Iniciando servidor FTP nativo em C#...");
                _ftpServerHost = _serviceProvider.GetRequiredService<IFtpServerHost>();
                await _ftpServerHost.StartAsync(cancellationToken).ConfigureAwait(false);
                _isRunning = true;
                _logger.LogInformation("[NEBULA-FTP-SERVER] Servidor FTP nativo em C# rodando com sucesso!");
            }
            catch (Exception ex)
            {
                _ftpServerHost = null;
                _isRunning = false;
                _logger.LogError(ex, "[NEBULA-FTP-SERVER] Falha ao iniciar o servidor FTP nativo em C#.");
                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <summary>
    /// Para o servidor FTP.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_isRunning || _ftpServerHost == null)
            {
                return;
            }

            try
            {
                _logger.LogInformation("[NEBULA-FTP-SERVER] Parando servidor FTP nativo em C#...");
                await _ftpServerHost.StopAsync(cancellationToken).ConfigureAwait(false);
                _isRunning = false;
                _ftpServerHost = null;
                _logger.LogInformation("[NEBULA-FTP-SERVER] Servidor FTP parado.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NEBULA-FTP-SERVER] Erro ao parar o servidor FTP nativo.");
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_isRunning)
        {
            await StopAsync().ConfigureAwait(false);
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        _lifecycleGate.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
