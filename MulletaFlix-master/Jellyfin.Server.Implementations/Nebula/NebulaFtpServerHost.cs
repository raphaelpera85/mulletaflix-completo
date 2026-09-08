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
            int ftpPort = 2121,
            int httpPort = 2123)
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
                opt.ServerAddress = "0.0.0.0";
                opt.Port = ftpPort > 0 ? ftpPort : 2121;
                opt.MaxActiveConnections = 0; // 0 = sem limite de conexões ativas
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
            _logger.LogError(ex, "[NEBULA-FTP-SERVER] Falha ao iniciar o servidor FTP nativo em C#.");
            throw;
        }
    }

    /// <summary>
    /// Para o servidor FTP.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
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
            _logger.LogInformation("[NEBULA-FTP-SERVER] Servidor FTP parado.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-FTP-SERVER] Erro ao parar o servidor FTP nativo.");
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

        _disposed = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
