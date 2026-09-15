using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Jellyfin.Server.Implementations.Nebula;
using MulletaFlix.Database.Implementations.Contexts;

namespace MulletaFlix.Server.Implementations.Nebula;

public sealed class NebulaFtpManager : INebulaFtpManager, IDisposable
{
    private readonly IServerConfigurationManager _configManager;
    private readonly ILogger<NebulaFtpManager> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILibraryManager? _libraryManager;
    private readonly NebulaMetadataExportService? _metadataExportService;
    private readonly IDbContextFactory<UsersDbContext>? _usersDbProvider;

    private readonly object _lock = new();
    private readonly List<string> _serverLogs = new();
    private readonly List<string> _downloaderLogs = new();
    private long _serverLogSeq;
    private long _downloaderLogSeq;
    private const int MaxLogLines = 600;

    private NebulaMongoContext? _mongoContext;
    private NebulaTelegramPool? _telegramPool;
    private NebulaUploadEngine? _uploadEngine;
    private NebulaFtpServerHost? _ftpServerHost;
    private NebulaHttpStreamServer? _httpStreamServer;
    private NebulaStagingWatcher? _stagingWatcher;
    private NebulaSupabaseSyncService? _supabaseSyncService;
    private NebulaDownloaderEngine? _downloaderEngine;
    private Process? _rcloneProcess;
    private CancellationTokenSource? _cleanupCts;
    private Task? _cleanupTask;

    private bool _isEnvioRunning;
    private bool _streamOnly;
    private bool _isDownloaderRunning;
    private bool _dependenciesChecked;
    private readonly SemaphoreSlim _envioLock = new(1, 1);
    private readonly SemaphoreSlim _downloaderLock = new(1, 1);
    private readonly SemaphoreSlim _sharedRuntimeLock = new(1, 1);
    private readonly SemaphoreSlim _maintenanceLock = new(1, 1);
    private readonly SemaphoreSlim _mountLock = new(1, 1);
    private readonly SemaphoreSlim _dependencyLock = new(1, 1);
    private long _nextAutomaticMountAttemptUtcTicks;
    private int _automaticMountAttemptInProgress;
    private int _automaticMountFailures;
    private int _mountRetryScheduled;
    private readonly Dictionary<string, NebulaOperationReplay> _operationReplays = new(StringComparer.Ordinal);
    private static readonly TimeSpan OperationReplayTtl = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, NebulaWorkerItemDto> _activeUploads = new(StringComparer.OrdinalIgnoreCase);
    private readonly NebulaDownloadStatusDto _currentDownload = new();

    private sealed record NebulaOperationReplay(object Result, DateTime ExpiresAtUtc);

    private bool TryGetOperationReplay<T>(string operationName, string? idempotencyKey, out T result)
    {
        result = default!;
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }

        lock (_lock)
        {
            var cacheKey = $"{operationName}:{idempotencyKey}";
            if (!_operationReplays.TryGetValue(cacheKey, out var replay))
            {
                return false;
            }

            if (replay.ExpiresAtUtc <= DateTime.UtcNow)
            {
                _operationReplays.Remove(cacheKey);
                return false;
            }

            if (replay.Result is not T typedResult)
            {
                return false;
            }

            result = typedResult;
            return true;
        }
    }

    private void CacheOperationReplay<T>(string operationName, string? idempotencyKey, T result)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return;
        }

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            foreach (var expiredKey in _operationReplays
                .Where(pair => pair.Value.ExpiresAtUtc <= now)
                .Select(pair => pair.Key)
                .ToArray())
            {
                _operationReplays.Remove(expiredKey);
            }

            if (_operationReplays.Count >= 1024)
            {
                _operationReplays.Remove(_operationReplays.OrderBy(pair => pair.Value.ExpiresAtUtc).First().Key);
            }

            _operationReplays[$"{operationName}:{idempotencyKey}"] = new NebulaOperationReplay(result!, now + OperationReplayTtl);
        }
    }

    private async Task<bool> TryGetOperationReplayAsync<T>(string operationName, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (TryGetOperationReplay(operationName, idempotencyKey, out T cached))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return false;
        }

        NebulaMongoContext? temporaryContext = null;
        try
        {
            var mongo = _mongoContext;
            if (mongo == null)
            {
                var connectionString = Config.MongoDbConnectionString;
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return false;
                }

                temporaryContext = new NebulaMongoContext(connectionString, "ftp", _loggerFactory.CreateLogger<NebulaMongoContext>());
                mongo = temporaryContext;
            }

            var persisted = await mongo.GetOperationReplayAsync<T>(operationName, idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (persisted is null)
            {
                return false;
            }

            CacheOperationReplay(operationName, idempotencyKey, persisted);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-IDEMPOTENCY] Não foi possível consultar replay persistido de {Operation}.", operationName);
            return false;
        }
        finally
        {
            temporaryContext?.Dispose();
        }
    }

    private async Task CacheOperationReplayAsync<T>(string operationName, string? idempotencyKey, T result, CancellationToken cancellationToken)
    {
        CacheOperationReplay(operationName, idempotencyKey, result);
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return;
        }

        NebulaMongoContext? temporaryContext = null;
        try
        {
            var mongo = _mongoContext;
            if (mongo == null)
            {
                var connectionString = Config.MongoDbConnectionString;
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    return;
                }

                temporaryContext = new NebulaMongoContext(connectionString, "ftp", _loggerFactory.CreateLogger<NebulaMongoContext>());
                mongo = temporaryContext;
            }

            await mongo.SaveOperationReplayAsync(operationName, idempotencyKey, result, OperationReplayTtl, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-IDEMPOTENCY] Não foi possível persistir replay de {Operation}; cache de processo mantido.", operationName);
        }
        finally
        {
            temporaryContext?.Dispose();
        }
    }
    private NebulaOperationStatusDto _maintenanceOperation = new();

    private void BeginMaintenanceOperation(string name)
    {
        lock (_lock)
        {
            _maintenanceOperation = new NebulaOperationStatusDto
            {
                Name = name,
                State = "running",
                StartedAtUtc = DateTime.UtcNow,
                ProgressPercent = 0
            };
        }
    }

    private void UpdateMaintenanceOperationProgress(string message)
    {
        lock (_lock)
        {
            _maintenanceOperation.ProgressText = message;
            var progressMatch = Regex.Match(message, @"Progresso dos arquivos:\s*(\d+)\/(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (progressMatch.Success
                && double.TryParse(progressMatch.Groups[1].Value, out var completed)
                && double.TryParse(progressMatch.Groups[2].Value, out var total)
                && total > 0)
            {
                _maintenanceOperation.ProgressPercent = Math.Round(Math.Clamp(completed / total * 100, 0, 100), 1);
            }
        }
    }

    private void CompleteMaintenanceOperation(string state, string error = "")
    {
        lock (_lock)
        {
            var finishedAt = DateTime.UtcNow;
            _maintenanceOperation.State = state;
            _maintenanceOperation.FinishedAtUtc = finishedAt;
            _maintenanceOperation.DurationMs = _maintenanceOperation.StartedAtUtc.HasValue
                ? Math.Max(0, (long)(finishedAt - _maintenanceOperation.StartedAtUtc.Value).TotalMilliseconds)
                : null;
            _maintenanceOperation.Error = error.Length > 1024 ? error[..1024] : error;
        }
    }

    private NebulaOperationStatusDto GetMaintenanceOperation()
    {
        lock (_lock)
        {
            return new NebulaOperationStatusDto
            {
                Name = _maintenanceOperation.Name,
                State = _maintenanceOperation.State,
                StartedAtUtc = _maintenanceOperation.StartedAtUtc,
                FinishedAtUtc = _maintenanceOperation.FinishedAtUtc,
                DurationMs = _maintenanceOperation.DurationMs,
                Error = _maintenanceOperation.Error,
                ProgressPercent = _maintenanceOperation.ProgressPercent,
                ProgressText = _maintenanceOperation.ProgressText
            };
        }
    }

    private void EmitServerLog(string level, string message)
    {
        var now = DateTime.Now;
        var formatted = $"[{now:HH:mm:ss}] [SERVER] {now:yyyy-MM-dd HH:mm:ss,fff} - {level} - {message}";
        AddServerLog(formatted);
    }

    private void EmitCleanupLog(string message)
    {
        var now = DateTime.Now;
        var formatted = $"[{now:HH:mm:ss}] [CLEANUP] {message}";
        AddServerLog(formatted);
    }

    private void EmitRawLog(string message)
    {
        var now = DateTime.Now;
        var formatted = $"[{now:HH:mm:ss}] {message}";
        AddServerLog(formatted);
    }

    public NebulaFtpManager(
        IServerConfigurationManager configManager,
        ILogger<NebulaFtpManager> logger,
        ILoggerFactory loggerFactory,
        ILibraryManager? libraryManager = null,
        NebulaMetadataExportService? metadataExportService = null,
        IDbContextFactory<UsersDbContext>? usersDbProvider = null)
    {
        _configManager = configManager;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _libraryManager = libraryManager;
        _metadataExportService = metadataExportService;
        _usersDbProvider = usersDbProvider;
    }

    private async Task EnsureRuntimeDependenciesAsync(CancellationToken cancellationToken)
    {
        if (_dependenciesChecked)
        {
            return;
        }

        await _dependencyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_dependenciesChecked)
            {
                return;
            }

            foreach (var scriptName in new[]
            {
                "install-python-if-missing.ps1",
                "install-mongodb-if-missing.ps1",
                "install-rclone-if-missing.ps1",
                "install-winfsp-if-missing.ps1"
            })
            {
                var scriptPath = Path.Combine(AppContext.BaseDirectory, scriptName);
                if (File.Exists(scriptPath))
                {
                    await RunPowerShellDependencyScriptAsync(scriptPath, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogDebug("[NEBULA-DEPS] Script não encontrado: {ScriptPath}", scriptPath);
                }
            }

            _dependenciesChecked = true;
            AddServerLog("[NEBULA-DEPS] Dependências de runtime verificadas.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-DEPS] Falha ao verificar/instalar dependências de runtime.");
            AddServerLog($"[NEBULA-DEPS] Falha ao verificar dependências: {ex.Message}");
        }
        finally
        {
            _dependencyLock.Release();
        }
    }

    private async Task RunPowerShellDependencyScriptAsync(string scriptPath, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);

        using var process = new Process { StartInfo = startInfo };
        _logger.LogInformation("[NEBULA-DEPS] Verificando {ScriptName}...", Path.GetFileName(scriptPath));
        if (!process.Start())
        {
            throw new InvalidOperationException("Não foi possível iniciar o PowerShell.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(output))
        {
            _logger.LogInformation("[NEBULA-DEPS] {Output}", output.Trim());
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(scriptPath)} terminou com código {process.ExitCode}: {error.Trim()}");
        }
    }

    internal void RemoveMonitoredMediaLibraryPaths(NebulaFtpConfiguration config)
    {
        if (_libraryManager == null)
        {
            return;
        }

        var roots = (config.MonitorPaths ?? Array.Empty<string>())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path.Trim()))
            .ToArray();
        if (roots.Length == 0)
        {
            return;
        }

        foreach (var library in _libraryManager.GetVirtualFolders())
        {
            var locationsToRemove = library.Locations
                .Where(loc => roots.Any(root =>
                {
                    try
                    {
                        var fullLoc = Path.GetFullPath(loc);
                        var cleanRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        return string.Equals(fullLoc, cleanRoot, StringComparison.OrdinalIgnoreCase) ||
                               fullLoc.StartsWith(cleanRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        return false;
                    }
                }))
                .ToList();

            foreach (var loc in locationsToRemove)
            {
                try
                {
                    _libraryManager.RemoveMediaPath(library.Name, loc);
                    _logger.LogInformation("[NEBULA-LIBRARY] Caminho monitorado do Nebula removido da biblioteca {Library}: {Path}", library.Name, loc);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NEBULA-LIBRARY] Não foi possível remover caminho monitorado {Path} da biblioteca {Library}", loc, library.Name);
                }
            }
        }
    }

    private NebulaFtpConfiguration Config =>
        _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp") ?? new NebulaFtpConfiguration();

    private static IEnumerable<string> GetDotEnvCandidates()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Projetos", "nebula", "NebulaFTP-master", ".env")
        };

        var explicitPath = Environment.GetEnvironmentVariable("NEBULA_ENV_FILE");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            candidates.Add(explicitPath.Trim());
        }

        return candidates;
    }

    private static Dictionary<string, string> ReadDotEnv()
    {
        foreach (var path in GetDotEnvCandidates())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var separator = line.IndexOf('=', StringComparison.Ordinal);
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim().Trim('"', '\'');
                values[key] = value;
            }

            return values;
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private NebulaFtpConfiguration ImportDotEnvConfiguration(NebulaFtpConfiguration config, bool save)
    {
        var env = ReadDotEnv();
        var changed = false;

        void SetIfMissing(string property, string key, Action<string> setter)
        {
            if (!env.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var current = property switch
            {
                nameof(NebulaFtpConfiguration.ApiId) => config.ApiId,
                nameof(NebulaFtpConfiguration.ApiHash) => config.ApiHash,
                nameof(NebulaFtpConfiguration.ChatId) => config.ChatId,
                nameof(NebulaFtpConfiguration.BotTokens) => config.BotTokens,
                _ => string.Empty
            };
            var missingOrInvalid = string.IsNullOrWhiteSpace(current);
            if (property == nameof(NebulaFtpConfiguration.ApiHash))
            {
                missingOrInvalid |= current.Trim().Length != 32;
            }
            else if (property == nameof(NebulaFtpConfiguration.ApiId))
            {
                missingOrInvalid |= !int.TryParse(current, out var parsedApiId) || parsedApiId <= 0;
            }
            if (missingOrInvalid)
            {
                setter(value);
                changed = true;
            }
        }

        SetIfMissing(nameof(NebulaFtpConfiguration.ApiId), "API_ID", value => config.ApiId = value);
        SetIfMissing(nameof(NebulaFtpConfiguration.ApiHash), "API_HASH", value => config.ApiHash = value);
        SetIfMissing(nameof(NebulaFtpConfiguration.ChatId), "CHAT_ID", value => config.ChatId = value);
        SetIfMissing(nameof(NebulaFtpConfiguration.BotTokens), "BOT_TOKENS", value => config.BotTokens = value);

        if (changed && save)
        {
            _configManager.SaveConfiguration("nebulaftp", config);
            AddServerLog("[NEBULA-CONFIG] Credenciais do .env importadas automaticamente.");
        }

        return config;
    }

    private string GetSessionsDirectory()
    {
        var dir = Path.Combine(_configManager.CommonApplicationPaths.DataPath, "nebula_sessions");
        Directory.CreateDirectory(dir);

        var probePath = Path.Combine(dir, $".write-test-{Guid.NewGuid():N}");
        try
        {
            using (new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            throw new InvalidOperationException($"O diretório de sessões do Nebula não é gravável: {dir}", ex);
        }

        return dir;
    }

    public async Task<NebulaStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var config = Config;
        var isDriveNMounted = IsDriveNAccessible();

        var status = new NebulaStatusDto
        {
            IsEnvioRunning = _isEnvioRunning && _ftpServerHost != null && _ftpServerHost.IsRunning,
            StreamOnly = _streamOnly,
            IsDownloaderRunning = _isDownloaderRunning && _downloaderEngine != null && _downloaderEngine.IsRunning,
            TurboActive = config.TurboEnabled,
            IsDriveNMounted = isDriveNMounted,
            DriveNStatus = isDriveNMounted ? "Unidade N: Montada" : "Unidade N: Desconectada"
        };

        var envioText = status.IsEnvioRunning
            ? (status.StreamOnly ? "Somente Streaming" : "Executando")
            : "Parado";
        var dlText = status.IsDownloaderRunning
            ? "Executando (1 mídia por vez)"
            : "Parado";
        var driveText = isDriveNMounted ? "Montado" : "Desconectado";

        status.GlobalStatusText = $"Envio: {envioText} | Disco N: {driveText} | Download: {dlText}";

        // Calculate Stage Disks
        var diskList = new List<NebulaStageDiskDto>();
        var stagePaths = config.StagePaths ?? Array.Empty<string>();
        foreach (var path in stagePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            try
            {
                var root = Path.GetPathRoot(path);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    var drive = new DriveInfo(root);
                    if (drive.IsReady)
                    {
                        var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                        var totalGb = drive.TotalSize / (1024.0 * 1024 * 1024);
                        var freePct = totalGb > 0 ? (freeGb / totalGb) * 100 : 0;
                        diskList.Add(new NebulaStageDiskDto
                        {
                            Path = path,
                            FreeGb = Math.Round(freeGb, 1),
                            TotalGb = Math.Round(totalGb, 1),
                            FreePercent = Math.Round(freePct, 0),
                            Formatted = $"{path}: {freeGb:F1} GB livres ({freePct:F0}%)"
                        });
                    }
                }
            }
            catch
            {
                diskList.Add(new NebulaStageDiskDto
                {
                    Path = path,
                    Formatted = $"{path} (Desconhecido)"
                });
            }
        }

        status.StageDisks = diskList;
        status.StageDisksFormatted = string.Join(" | ", diskList.Select(d => d.Formatted));
        status.MaintenanceOperation = GetMaintenanceOperation();

        // Copy download status
        lock (_lock)
        {
            status.CurrentDownload = new NebulaDownloadStatusDto
            {
                Name = _currentDownload.Name,
                StageStep = _currentDownload.StageStep,
                Percentage = _currentDownload.Percentage,
                DoneMb = _currentDownload.DoneMb,
                TotalMb = _currentDownload.TotalMb,
                Speed = _currentDownload.Speed,
                DetailText = _currentDownload.DetailText
            };
        }

        // Query active uploads (in-memory real-time first, fallback to Mongo)
        if (status.IsEnvioRunning)
        {
            status.QueuedUploads = await QueryMongoQueuedUploadsAsync(config, cancellationToken).ConfigureAwait(false);
            status.UploadQueueCount = status.QueuedUploads.Count;
            var mongoActive = await QueryMongoActiveUploadsAsync(config, cancellationToken).ConfigureAwait(false);
            if (mongoActive.Count > 0)
            {
                status.ActiveUploads = mongoActive;
            }
            else
            {
                status.ActiveUploads = _activeUploads.Values
                    .Where(u => string.Equals(u.Status, "uploading", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(u => u.WorkerId)
                    .ToList();
            }
        }

        return status;
    }

    public async Task<NebulaComponentHealthDto> GetComponentHealthAsync(CancellationToken cancellationToken = default)
    {
        var config = Config;
        var health = new NebulaComponentHealthDto
        {
            MongoConfigured = !string.IsNullOrWhiteSpace(config.MongoDbConnectionString),
            TelegramConfigured = !string.IsNullOrWhiteSpace(config.ApiId)
                && !string.IsNullOrWhiteSpace(config.ApiHash)
                && !string.IsNullOrWhiteSpace(config.BotTokens),
            FtpListenerRunning = _isEnvioRunning && _ftpServerHost?.IsRunning == true,
            HttpListenerRunning = _isEnvioRunning && _httpStreamServer?.IsRunning == true,
            TelegramReady = _telegramPool?.IsInitialized == true && _telegramPool.AvailableBotCount > 0,
            TelegramAvailableBots = _telegramPool?.AvailableBotCount ?? 0
        };

        if (!health.MongoConfigured)
        {
            health.MongoStatus = "connection string não configurada";
            return health;
        }

        if (_mongoContext == null)
        {
            health.MongoStatus = "contexto não inicializado";
            return health;
        }

        try
        {
            await _mongoContext.PingAsync(cancellationToken).ConfigureAwait(false);
            health.MongoConnected = true;
            health.MongoStatus = "conectado";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            health.MongoStatus = $"indisponível: {ex.GetType().Name}";
            _logger.LogWarning(ex, "[NEBULA-HEALTH] MongoDB ping falhou.");
        }

        return health;
    }

    private async Task<List<NebulaWorkerItemDto>> QueryMongoActiveUploadsAsync(
        NebulaFtpConfiguration config,
        CancellationToken cancellationToken)
    {
        var results = new List<NebulaWorkerItemDto>();
        try
        {
            NebulaMongoContext? tempMongo = null;
            var mongo = _mongoContext;
            if (mongo == null && !string.IsNullOrWhiteSpace(config.MongoDbConnectionString))
            {
                tempMongo = new NebulaMongoContext(config.MongoDbConnectionString, "ftp", _loggerFactory.CreateLogger<NebulaMongoContext>());
                mongo = tempMongo;
            }

            if (mongo == null)
            {
                return results;
            }

            try
            {
                var docs = await mongo.GetActiveUploadsAsync(cancellationToken).ConfigureAwait(false);
                foreach (var d in docs)
                {
                    var name = d.Contains("name") ? d["name"].AsString : string.Empty;
                    var disp = d.Contains("display_name") ? d["display_name"].AsString : name;
                    var st = d.Contains("status") ? d["status"].AsString : "uploading";
                    var size = d.Contains("size") && d["size"].IsNumeric ? d["size"].ToInt64() : 0L;
                    var uploaded = d.Contains("uploaded_bytes") && d["uploaded_bytes"].IsNumeric ? d["uploaded_bytes"].ToInt64() : 0L;
                    var workerId = d.Contains("worker_id") ? d["worker_id"].ToString() ?? "?" : "?";

                    var bots = new List<int>();
                    if (d.Contains("parts") && d["parts"].IsBsonArray)
                    {
                        foreach (var p in d["parts"].AsBsonArray)
                        {
                            if (p.IsBsonDocument && p.AsBsonDocument.Contains("bot_index") && p.AsBsonDocument["bot_index"].IsInt32)
                            {
                                var botIdx = p.AsBsonDocument["bot_index"].AsInt32;
                                if (!bots.Contains(botIdx))
                                {
                                    bots.Add(botIdx);
                                }
                            }
                        }
                        bots.Sort();
                    }

                    var botText = bots.Count > 0
                        ? "Bots " + string.Join(", ", bots.Take(4).Select(b => $"#{b}")) + (bots.Count > 4 ? $" +{bots.Count - 4}" : string.Empty)
                        : "Bots rotativos";

                    double pct = 0;
                    if (uploaded > 0 && size > 0)
                    {
                        pct = (uploaded / (double)size) * 100.0;
                    }

                    var infoText = st == "uploading"
                        ? $"Uploading | Worker #{workerId} | {botText} | {disp}"
                        : $"Na fila para envio | {disp}";

                    results.Add(new NebulaWorkerItemDto
                    {
                        Name = name,
                        DisplayName = disp,
                        Status = st,
                        WorkerId = workerId,
                        BotText = botText,
                        Percentage = Math.Round(pct, 1),
                        Size = size,
                        UploadedBytes = uploaded,
                        InfoText = infoText
                    });
                }
            }
            finally
            {
                tempMongo?.Dispose();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao consultar uploads ativos no MongoDB em C#");
        }

        return results;
    }

    private async Task<List<NebulaWorkerItemDto>> QueryMongoQueuedUploadsAsync(
        NebulaFtpConfiguration config,
        CancellationToken cancellationToken)
    {
        var results = new List<NebulaWorkerItemDto>();
        try
        {
            NebulaMongoContext? tempMongo = null;
            var mongo = _mongoContext;
            if (mongo == null && !string.IsNullOrWhiteSpace(config.MongoDbConnectionString))
            {
                tempMongo = new NebulaMongoContext(config.MongoDbConnectionString, "ftp", _loggerFactory.CreateLogger<NebulaMongoContext>());
                mongo = tempMongo;
            }

            if (mongo == null)
            {
                return results;
            }

            try
            {
                var docs = await mongo.GetQueuedUploadsAsync(cancellationToken).ConfigureAwait(false);
                foreach (var d in docs)
                {
                    var name = d.Contains("name") ? d["name"].AsString : string.Empty;
                    var size = d.Contains("size") && d["size"].IsNumeric ? d["size"].ToInt64() : 0L;
                    results.Add(new NebulaWorkerItemDto
                    {
                        Name = name,
                        DisplayName = name,
                        Status = "queued",
                        WorkerId = "fila",
                        InfoText = $"Na fila para envio | {name}",
                        Size = size
                    });
                }
            }
            finally
            {
                tempMongo?.Dispose();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao consultar fila de uploads no MongoDB em C#");
        }

        return results;
    }

    public NebulaLogsDto GetLogs(int serverOffset, int downloaderOffset)
    {
        lock (_lock)
        {
            var serverTotal = (int)_serverLogSeq;
            List<string> serverSlice;
            if (serverOffset <= 0)
            {
                serverSlice = _serverLogs.ToList();
            }
            else if (serverOffset >= serverTotal)
            {
                serverSlice = new List<string>();
            }
            else
            {
                var diff = serverTotal - serverOffset;
                var takeCount = Math.Min(diff, _serverLogs.Count);
                serverSlice = _serverLogs.Skip(_serverLogs.Count - takeCount).ToList();
            }

            var dlTotal = (int)_downloaderLogSeq;
            List<string> dlSlice;
            if (downloaderOffset <= 0)
            {
                dlSlice = _downloaderLogs.ToList();
            }
            else if (downloaderOffset >= dlTotal)
            {
                dlSlice = new List<string>();
            }
            else
            {
                var diff = dlTotal - downloaderOffset;
                var takeCount = Math.Min(diff, _downloaderLogs.Count);
                dlSlice = _downloaderLogs.Skip(_downloaderLogs.Count - takeCount).ToList();
            }

            return new NebulaLogsDto
            {
                ServerLogs = serverSlice,
                ServerLogTotal = serverTotal,
                DownloaderLogs = dlSlice,
                DownloaderLogTotal = dlTotal
            };
        }
    }

    public async Task<bool> StartEnvioAsync(bool streamOnly, CancellationToken cancellationToken = default)
    {
        if (_isEnvioRunning && _ftpServerHost != null && _ftpServerHost.IsRunning)
        {
            return true;
        }

        if (!await _envioLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            AddServerLog("[NEBULA] Inicialização do Envio já está em andamento. Aguarde a autenticação dos bots...");
            return true;
        }

        var createdMongoContext = false;
        var createdTelegramPool = false;

        try
        {
            if (_isEnvioRunning && _ftpServerHost != null && _ftpServerHost.IsRunning)
            {
                return true;
            }

            await EnsureRuntimeDependenciesAsync(cancellationToken).ConfigureAwait(false);
            var config = ImportDotEnvConfiguration(NormalizeRuntimeConfiguration(Config), save: true);
            if (!config.AllowInsecureRemoteFtp && !IsLoopbackHost(config.ServerHost))
            {
                throw new InvalidOperationException(
                    "NebulaFTP nativo não suporta FTPS. Use ServerHost=127.0.0.1/localhost ou habilite AllowInsecureRemoteFtp explicitamente para uma rede confiável.");
            }

            _configManager.SaveConfiguration("nebulaftp", config);
            RemoveMonitoredMediaLibraryPaths(config);
            _activeUploads.Clear();
            EmitRawLog(streamOnly ? "Iniciando NebulaFTP Server (Modo Somente Streaming)." : "Iniciando NebulaFTP Server (Modo Envio de Mídias).");

            StartContinuousCleanup(config);

            // 1. Contexto do MongoDB
            if (_mongoContext == null)
            {
                _mongoContext = new NebulaMongoContext(config.MongoDbConnectionString, "ftp", _loggerFactory.CreateLogger<NebulaMongoContext>());
                createdMongoContext = true;
            }

            await _mongoContext.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);

            // 1.1 Sincronizador com Supabase e verificação de prioridade do banco (restauração se Mongo estiver vazio)
            if (!string.IsNullOrWhiteSpace(config.SupabaseUrl) && !string.IsNullOrWhiteSpace(config.SupabaseKey))
            {
                _supabaseSyncService = new NebulaSupabaseSyncService(
                    _mongoContext,
                    _loggerFactory.CreateLogger<NebulaSupabaseSyncService>(),
                    _usersDbProvider);
            }

            await EnsureDatabaseRestoredIfEmptyAsync(config, AddServerLog, cancellationToken).ConfigureAwait(false);

            // O provedor FTP autentica no MongoDB assim que o socket abre.
            // Portanto, a conta usada pelo rclone precisa existir antes de
            // iniciar o servidor FTP; criá-la durante o mount é tarde demais.
            var (ftpUsername, ftpPassword) = EnsureLocalFtpCredentials(config);
            var ftpPasswordHash = BCrypt.Net.BCrypt.HashPassword(ftpPassword);
            await _mongoContext.UpsertUserAsync(ftpUsername, ftpPasswordHash, "elradfmwM", cancellationToken).ConfigureAwait(false);

            // 2. Telegram MTProto Pool C#
            Task? poolInitTask = null;
            if (_telegramPool == null)
            {
                var botTokens = await LoadBotTokensAsync(config, cancellationToken).ConfigureAwait(false);

                _ = int.TryParse(config.ApiId, out var apiId);
                _ = long.TryParse(config.ChatId, out var chatId);

                _telegramPool = new NebulaTelegramPool(apiId, config.ApiHash, botTokens, chatId, GetSessionsDirectory(), _loggerFactory.CreateLogger<NebulaTelegramPool>());
                createdTelegramPool = true;
                poolInitTask = _telegramPool.InitializeAsync(EmitServerLog, cancellationToken);
            }

            EmitServerLog("INFO", "Índices MongoDB verificados.");
            EmitServerLog("WARNING", "FTPS disabled. Plain FTP must not be exposed beyond a trusted network.");
            EmitServerLog("INFO", $"🚀 Nebula FTP (MonoBot) Rodando na porta {config.ServerPort}");
            EmitServerLog("INFO", $"HTTP Stream local: http://127.0.0.1:{config.HttpStreamPort}");

            Func<string, Task> logQueueState = async evt =>
            {
                if (_mongoContext != null)
                {
                    var stats = await _mongoContext.GetQueueStatsAsync(CancellationToken.None).ConfigureAwait(false);
                    EmitServerLog("INFO", $"Fila: evento={evt} stage={stats.Staging} fila={stats.Queued} enviando={stats.Uploading} enviados={stats.Completed} falhas={stats.Failed} faltam={stats.Pending} (no disco={stats.PendingDisk})");
                }
            };

            // 3. Upload Engine C# com callback de sincronização imediata com Supabase
            // Em modo streamOnly, NÃO cria upload engine (bots não enviam arquivos, só servem streaming)
            if (!streamOnly)
            {
                _uploadEngine = new NebulaUploadEngine(
                    _mongoContext,
                    _telegramPool,
                    config.MaxWorkers,
                    config.ChunkSizeMb,
                    config.DeleteSourceAfterUpload,
                    _loggerFactory.CreateLogger<NebulaUploadEngine>(),
                    AddServerLog,
                    onNodeUpdated: async doc =>
                    {
                        if (_supabaseSyncService != null && !string.IsNullOrWhiteSpace(config.SupabaseUrl) && !string.IsNullOrWhiteSpace(config.SupabaseKey))
                        {
                            await _supabaseSyncService.SyncSingleNodeAsync(config.SupabaseUrl, config.SupabaseKey, doc, CancellationToken.None).ConfigureAwait(false);
                        }
                    },
                    emitServerLog: EmitServerLog,
                    logQueueState: logQueueState);
            }
            else
            {
                // Modo streamOnly: uploadEngine nulo, bots só servem streaming via FTP/HTTP
                _uploadEngine = null;
                EmitServerLog("INFO", "[NEBULA] Modo Somente Streaming: Upload Engine desativado (bots não enviam arquivos).");
            }

            // 4. Servidor FTP C# nativo
            _ftpServerHost = new NebulaFtpServerHost(
                _mongoContext,
                _telegramPool,
                _uploadEngine, // pode ser null em streamOnly
                _loggerFactory.CreateLogger<NebulaFtpServerHost>(),
                _loggerFactory.CreateLogger<NebulaFileSystem>(),
                _loggerFactory.CreateLogger<NebulaFtpMembershipProvider>(),
                config.ServerHost,
                config.ServerPort,        // FTP port (default 2121)
                config.HttpStreamPort,   // HTTP Stream port (default 2123 for MulletaFlix)
                config.MaxActiveConnections);
            await _ftpServerHost.StartAsync(cancellationToken).ConfigureAwait(false);

            // 4.1 Servidor HTTP Stream C# nativo (porta 2123 para playback STRM e streaming direto)
            _httpStreamServer = new NebulaHttpStreamServer(
                _mongoContext,
                _telegramPool,
                config.ServerHost,
                config.HttpStreamPort,    // HTTP Stream port (default 2123 for MulletaFlix)
                _loggerFactory.CreateLogger<NebulaHttpStreamServer>(),
                config.HttpStreamToken,
                config.MaxActiveConnections);
            _httpStreamServer.Start();

            if (poolInitTask != null)
            {
                await poolInitTask.ConfigureAwait(false);
            }

            // 5. Watcher de diretórios de Staging
            if (!streamOnly)
            {
                var allWatchDirs = new List<string>();
                if (config.StagePaths != null)
                {
                    allWatchDirs.AddRange(config.StagePaths.Where(p => !string.IsNullOrWhiteSpace(p)));
                }

                if (allWatchDirs.Count == 0)
                {
                    allWatchDirs.Add(Path.Combine(AppContext.BaseDirectory, "NebulaStage"));
                }

                _stagingWatcher = new NebulaStagingWatcher(_uploadEngine, _mongoContext, _loggerFactory.CreateLogger<NebulaStagingWatcher>(), EmitServerLog, _telegramPool.BotCount);
                _stagingWatcher.Start(allWatchDirs, config.MaxWorkers);
            }

            // 6. Inicia sincronização contínua de background para manter o Supabase sempre atualizado
            if (_supabaseSyncService != null && !string.IsNullOrWhiteSpace(config.SupabaseUrl) && !string.IsNullOrWhiteSpace(config.SupabaseKey))
            {
                if (config.SupabaseAutoBackup)
                {
                    var intervalMinutes = Math.Max(15, config.SupabaseAutoBackupIntervalHours * 60);
                    _supabaseSyncService.StartContinuousSync(config.SupabaseUrl, config.SupabaseKey, intervalMinutes: intervalMinutes, progressAction: AddServerLog);
                    AddServerLog($"[SUPABASE] Serviço de sincronização contínua e backup automático ativado (Intervalo: {config.SupabaseAutoBackupIntervalHours} horas).");
                }
                else
                {
                    AddServerLog("[SUPABASE] Backup automático em segundo plano desativado na configuração (sincronização de novos nós em tempo real permanece ativa).");
                }
            }

            _isEnvioRunning = true;
            _streamOnly = streamOnly;

            // 7. A montagem inicial é coordenada pelo NebulaHostedService depois
            // que o servidor FTP estiver pronto. Não dispare outra montagem em
            // paralelo aqui: isso pode deixar o rclone apontando para um FTP que
            // ainda está subindo e causar erros de E/S durante a varredura do Jellyfin.
            // O modo streamOnly é iniciado pelo próprio MountDriveNAsync, portanto
            // também não pode iniciar uma montagem recursiva neste ponto.
            if (config.UseMappedDrive && !streamOnly)
            {
                AddServerLog("[NEBULA-MOUNT] Montagem N: será coordenada pelo serviço de startup após o FTP ficar pronto.");
            }
            else
            {
                AddServerLog(config.UseMappedDrive
                    ? "[NEBULA-MOUNT] Modo Somente Streaming: montagem N: será concluída pelo fluxo que solicitou o mount."
                    : "[NEBULA-MOUNT] UseMappedDrive=false: pulando montagem automática da unidade N:. Streaming via HTTP/FTP direto.");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start NebulaFTP server");
            AddServerLog($"[ERRO] Falha ao iniciar NebulaFTP nativo: {ex.Message}");
            _isEnvioRunning = false;

            // Libere apenas os recursos criados nesta tentativa. O pool/Mongo podem
            // estar sendo compartilhados pelo Downloader STRM.
            try
            {
                if (!_isDownloaderRunning)
                {
                    await DisposeSharedRuntimeResourcesAsync().ConfigureAwait(false);
                }

                _supabaseSyncService?.Dispose();
                _supabaseSyncService = null;
                if (_stagingWatcher != null)
                {
                    await _stagingWatcher.StopAsync().ConfigureAwait(false);
                    await _stagingWatcher.DisposeAsync().ConfigureAwait(false);
                    _stagingWatcher = null;
                }

                if (_ftpServerHost != null)
                {
                    await _ftpServerHost.StopAsync(CancellationToken.None).ConfigureAwait(false);
                    await _ftpServerHost.DisposeAsync().ConfigureAwait(false);
                    _ftpServerHost = null;
                }

                if (_httpStreamServer != null)
                {
                    await _httpStreamServer.DisposeAsync().ConfigureAwait(false);
                    _httpStreamServer = null;
                }

                if (_uploadEngine != null)
                {
                    await _uploadEngine.DisposeAsync().ConfigureAwait(false);
                    _uploadEngine = null;
                }

                if (createdTelegramPool && _telegramPool != null)
                {
                    await _telegramPool.DisposeAsync().ConfigureAwait(false);
                    _telegramPool = null;
                }

                if (createdMongoContext && _mongoContext != null)
                {
                    _mongoContext.Dispose();
                    _mongoContext = null;
                }
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(cleanupException, "Failed to clean up after NebulaFTP startup failure");
            }

            return false;
        }
        finally
        {
            _envioLock.Release();
        }
    }

    public async Task<bool> StopEnvioAsync(CancellationToken cancellationToken = default)
    {
        await _envioLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _isEnvioRunning = false;
            _activeUploads.Clear();

            // Only unmount drive N: if NO service (Envio or Downloader) needs it anymore
            if (!_isDownloaderRunning)
            {
                try
                {
                    await UnmountDriveNAsync(cancellationToken).ConfigureAwait(false);
                    AddServerLog("[NEBULA-MOUNT] Unidade N: desmontada (Envio parado e Downloader inativo).");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Erro ao desmontar unidade N: no StopEnvio");
                }
            }
            else
            {
                AddServerLog("[NEBULA-MOUNT] Unidade N: mantida montada pois Downloader ainda está ativo.");
            }

            await _sharedRuntimeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_supabaseSyncService != null)
                {
                    _supabaseSyncService.Dispose();
                    _supabaseSyncService = null;
                }
            }
            finally
            {
                _sharedRuntimeLock.Release();
            }

            if (_stagingWatcher != null)
            {
                await _stagingWatcher.StopAsync().ConfigureAwait(false);
                await _stagingWatcher.DisposeAsync().ConfigureAwait(false);
                _stagingWatcher = null;
            }

            if (_ftpServerHost != null)
            {
                await _ftpServerHost.StopAsync(cancellationToken).ConfigureAwait(false);
                await _ftpServerHost.DisposeAsync().ConfigureAwait(false);
                _ftpServerHost = null;
            }

            if (_httpStreamServer != null)
            {
                await _httpStreamServer.DisposeAsync().ConfigureAwait(false);
                _httpStreamServer = null;
            }

            if (_uploadEngine != null)
            {
                await _uploadEngine.DisposeAsync().ConfigureAwait(false);
                _uploadEngine = null;
            }

            AddServerLog("NebulaFTP Server nativo em C# encerrado com sucesso.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop NebulaFTP server");
            AddServerLog($"[ERRO] Falha ao parar serviços: {ex.Message}");
            return false;
        }
        finally
        {
            try
            {
                await DisposeSharedRuntimeResourcesAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(cleanupException, "Falha ao liberar recursos compartilhados após parar o Envio.");
            }

            _envioLock.Release();
        }
    }

    public async Task<bool> StartDownloaderAsync(CancellationToken cancellationToken = default)
    {
        if (_isDownloaderRunning && _downloaderEngine != null && _downloaderEngine.IsRunning)
        {
            return true;
        }

        if (!await _downloaderLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            AddDownloaderLog("[NEBULA] Inicialização do Downloader já está em andamento.");
            return true;
        }

        NebulaFtpConfiguration config;
        try
        {
            config = ImportDotEnvConfiguration(NormalizeRuntimeConfiguration(Config), save: true);
            _configManager.SaveConfiguration("nebulaftp", config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Nebula downloader configuration");
            AddDownloaderLog($"[ERRO] Falha ao carregar configuração do Downloader: {ex.Message}");
            _downloaderLock.Release();
            return false;
        }

        var monitorSources = (config.MonitorPaths ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (monitorSources.Count == 0)
        {
            _downloaderLock.Release();
            AddDownloaderLog("[ERRO] Nenhuma pasta de monitoramento configurada.");
            return false;
        }

        // StartEnvioAsync and StartDownloaderAsync share the Mongo context and
        // Telegram pool. Serialize their startup paths so both cannot create
        // duplicate shared resources at the same time.
        bool runtimeLockAcquired;
        try
        {
            runtimeLockAcquired = _envioLock.Wait(0, cancellationToken);
        }
        catch
        {
            _downloaderLock.Release();
            throw;
        }

        if (!runtimeLockAcquired)
        {
            _downloaderLock.Release();
            AddDownloaderLog("[NEBULA] Inicialização do Envio já está em andamento. Aguarde e tente o Downloader novamente.");
            return true;
        }

        try
        {
            if (_isDownloaderRunning && _downloaderEngine != null && _downloaderEngine.IsRunning)
            {
                return true;
            }

            await EnsureRuntimeDependenciesAsync(cancellationToken).ConfigureAwait(false);
            // Garante MongoDB e Telegram Pool
            _mongoContext ??= new NebulaMongoContext(config.MongoDbConnectionString, "ftp", _loggerFactory.CreateLogger<NebulaMongoContext>());

            // Verifica se MongoDB está vazio para restaurar do Supabase antes de iniciar o downloader
            await EnsureDatabaseRestoredIfEmptyAsync(config, AddDownloaderLog, cancellationToken).ConfigureAwait(false);

            if (_telegramPool == null)
            {
                var botTokens = await LoadBotTokensAsync(config, cancellationToken).ConfigureAwait(false);
                _ = int.TryParse(config.ApiId, out var apiId);
                _ = long.TryParse(config.ChatId, out var chatId);

                _telegramPool = new NebulaTelegramPool(apiId, config.ApiHash, botTokens, chatId, GetSessionsDirectory(), _loggerFactory.CreateLogger<NebulaTelegramPool>());
                await _telegramPool.InitializeAsync(emitLog: null, cancellationToken).ConfigureAwait(false);
            }

            _downloaderEngine = new NebulaDownloaderEngine(
                _mongoContext,
                _telegramPool,
                _loggerFactory.CreateLogger<NebulaDownloaderEngine>(),
                _metadataExportService);
            _downloaderEngine.OnLog += msg => AddDownloaderLog(msg);
            _downloaderEngine.OnProgressChanged += st =>
            {
                _currentDownload.Name = st.Name;
                _currentDownload.TotalMb = st.TotalMb;
                _currentDownload.DoneMb = st.DoneMb;
                _currentDownload.Percentage = st.Percentage;
                _currentDownload.StageStep = st.StageStep;
                _currentDownload.DetailText = st.DetailText;
            };

            _downloaderEngine.Start(config);
            _isDownloaderRunning = true;
            AddDownloaderLog("Iniciando STRM Downloader (1 mídia por vez)...");

            if (_cleanupTask == null)
            {
                StartContinuousCleanup(config);
            }

            if (!IsDriveNAccessible() && config.UseMappedDrive)
            {
                _ = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await MountDriveNAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Erro ao auto-montar unidade N: no startup do Downloader");
                        }
                    },
                    CancellationToken.None);
            }
            else if (!config.UseMappedDrive)
            {
                AddDownloaderLog("[NEBULA-MOUNT] UseMappedDrive=false: pulando montagem automática da unidade N:. Downloader opera via caminhos locais.");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start STRM Downloader");
            AddDownloaderLog($"[ERRO] Falha ao iniciar Downloader: {ex.Message}");
            _isDownloaderRunning = false;
            return false;
        }
        finally
        {
            _envioLock.Release();
            _downloaderLock.Release();
        }
    }

    public async Task<bool> StopDownloaderAsync(CancellationToken cancellationToken = default)
    {
        await _downloaderLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            _isDownloaderRunning = false;
            if (_downloaderEngine != null)
            {
                await _downloaderEngine.StopAsync().ConfigureAwait(false);
                await _downloaderEngine.DisposeAsync().ConfigureAwait(false);
                _downloaderEngine = null;
            }

            _currentDownload.Name = "Downloader parado.";
            _currentDownload.StageStep = string.Empty;
            _currentDownload.Percentage = 0;
            _currentDownload.DetailText = "0.0%";

            AddDownloaderLog("STRM Downloader encerrado.");

            // Only unmount drive N: if NO service (Envio or Downloader) needs it anymore
            if (!_isEnvioRunning)
            {
                try
                {
                    await UnmountDriveNAsync(cancellationToken).ConfigureAwait(false);
                    AddDownloaderLog("[NEBULA-MOUNT] Unidade N: desmontada (Downloader parado e Envio inativo).");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Erro ao desmontar unidade N: no StopDownloader");
                }
            }
            else
            {
                AddDownloaderLog("[NEBULA-MOUNT] Unidade N: mantida montada pois Envio ainda está ativo.");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop STRM Downloader");
            AddDownloaderLog($"[ERRO] Falha ao parar Downloader: {ex.Message}");
            return false;
        }
        finally
        {
            try
            {
                await DisposeSharedRuntimeResourcesAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(cleanupException, "Falha ao liberar recursos compartilhados após parar o Downloader.");
            }

            _downloaderLock.Release();
        }
    }

    private async Task DisposeSharedRuntimeResourcesAsync()
    {
        await _sharedRuntimeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isEnvioRunning || _isDownloaderRunning)
            {
                return;
            }

            var cleanupTask = _cleanupTask;
            _cleanupCts?.Cancel();

            if (cleanupTask != null)
            {
                try
                {
                    await Task.WhenAny(cleanupTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Erro ao aguardar encerramento da limpeza contínua do Nebula.");
                }
            }

            _cleanupCts?.Dispose();
            _cleanupCts = null;
            _cleanupTask = null;

            if (_telegramPool != null)
            {
                await _telegramPool.DisposeAsync().ConfigureAwait(false);
                _telegramPool = null;
            }

            if (_mongoContext != null)
            {
                _mongoContext.Dispose();
                _mongoContext = null;
            }
        }
        finally
        {
            _sharedRuntimeLock.Release();
        }
    }

    public async Task<bool> GenerateStrmAsync(string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        if (await TryGetOperationReplayAsync<bool>("generate-strm", idempotencyKey, cancellationToken).ConfigureAwait(false))
        {
            TryGetOperationReplay("generate-strm", idempotencyKey, out bool cached);
            return cached;
        }

        await _maintenanceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (await TryGetOperationReplayAsync<bool>("generate-strm", idempotencyKey, cancellationToken).ConfigureAwait(false))
        {
            _maintenanceLock.Release();
            TryGetOperationReplay("generate-strm", idempotencyKey, out bool cached);
            return cached;
        }
        BeginMaintenanceOperation("generate-strm");
        var operationTimer = Stopwatch.StartNew();
        _logger.LogInformation("[NEBULA-OPERATION] STRM generation started.");

        try
        {
            var config = NormalizeRuntimeConfiguration(Config);
            _configManager.SaveConfiguration("nebulaftp", config);
            AddServerLog("[STRM] Iniciando geração da biblioteca STRM em C# nativo...");

            using var mongo = new NebulaMongoContext(config.MongoDbConnectionString, "ftp", _loggerFactory.CreateLogger<NebulaMongoContext>());
            if (!string.IsNullOrWhiteSpace(config.SupabaseUrl) && !string.IsNullOrWhiteSpace(config.SupabaseKey))
            {
                var count = await mongo.CountFilesAsync(cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    AddServerLog("[STRM] MongoDB vazio detectado. Restaurando acervo do Supabase antes de gerar STRM...");
                    var sync = new NebulaSupabaseSyncService(mongo, _loggerFactory.CreateLogger<NebulaSupabaseSyncService>(), _usersDbProvider);
                    await sync.PerformRestoreAsync(config.SupabaseUrl, config.SupabaseKey, cancellationToken).ConfigureAwait(false);
                }
            }

            var generator = new NebulaStrmGenerator(mongo, _loggerFactory.CreateLogger<NebulaStrmGenerator>());
            await generator.GenerateAsync(config, msg => AddServerLog(msg), cancellationToken).ConfigureAwait(false);
            CompleteMaintenanceOperation("succeeded");
            await CacheOperationReplayAsync("generate-strm", idempotencyKey, true, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AddServerLog("[STRM] Geração cancelada.");
            CompleteMaintenanceOperation("cancelled");
            throw;
        }
        catch (Exception ex)
        {
            AddServerLog($"[STRM-ERRO] Falha ao gerar STRM: {ex.Message}");
            _logger.LogError(ex, "[NEBULA-STRM] Falha ao gerar STRM.");
            CompleteMaintenanceOperation("failed", ex.Message);
            await CacheOperationReplayAsync("generate-strm", idempotencyKey, false, CancellationToken.None).ConfigureAwait(false);
            return false;
        }
        finally
        {
            operationTimer.Stop();
            _logger.LogInformation("[NEBULA-OPERATION] STRM generation finished in {DurationMs} ms.", operationTimer.ElapsedMilliseconds);
            _maintenanceLock.Release();
        }
    }

    public async Task<bool> PruneCompletedAsync(string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        if (await TryGetOperationReplayAsync<bool>("prune-completed", idempotencyKey, cancellationToken).ConfigureAwait(false))
        {
            TryGetOperationReplay("prune-completed", idempotencyKey, out bool cached);
            return cached;
        }

        await _maintenanceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (await TryGetOperationReplayAsync<bool>("prune-completed", idempotencyKey, cancellationToken).ConfigureAwait(false))
        {
            _maintenanceLock.Release();
            TryGetOperationReplay("prune-completed", idempotencyKey, out bool cached);
            return cached;
        }
        BeginMaintenanceOperation("prune-completed");
        var operationTimer = Stopwatch.StartNew();
        _logger.LogInformation("[NEBULA-OPERATION] Completed-record cleanup started.");

        try
        {
            var config = Config;
            AddDownloaderLog("[LIMPEZA] Executando limpeza de registros inconsistentes no MongoDB...");

            using var mongo = new NebulaMongoContext(config.MongoDbConnectionString, "ftp", _loggerFactory.CreateLogger<NebulaMongoContext>());
            var deleted = await mongo.PruneCompletedAsync(cancellationToken).ConfigureAwait(false);
            AddDownloaderLog($"[LIMPEZA] Limpeza concluída: {deleted} registros corrigidos.");
            CompleteMaintenanceOperation("succeeded");
            await CacheOperationReplayAsync("prune-completed", idempotencyKey, true, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AddDownloaderLog("[LIMPEZA] Limpeza cancelada.");
            CompleteMaintenanceOperation("cancelled");
            throw;
        }
        catch (Exception ex)
        {
            AddDownloaderLog($"[LIMPEZA-ERRO] {ex.Message}");
            _logger.LogError(ex, "[NEBULA-CLEANUP] Erro durante a limpeza.");
            CompleteMaintenanceOperation("failed", ex.Message);
            await CacheOperationReplayAsync("prune-completed", idempotencyKey, false, CancellationToken.None).ConfigureAwait(false);
            return false;
        }
        finally
        {
            operationTimer.Stop();
            _logger.LogInformation(
                "[NEBULA-OPERATION] Completed-record cleanup finished in {DurationMs} ms.",
                operationTimer.ElapsedMilliseconds);
            _maintenanceLock.Release();
        }
    }


    private void StartContinuousCleanup(NebulaFtpConfiguration config)
    {
        _cleanupCts?.Cancel();
        _cleanupCts?.Dispose();
        _cleanupCts = new CancellationTokenSource();

        var sources = (config.MonitorPaths ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p) && !p.StartsWith("N:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (sources.Count == 0)
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, "midias");
            sources.Add(fallback);
        }

        var sourceListStr = string.Join(", ", sources.Select(s => $"'{s}'"));

        EmitCleanupLog("Bot de limpeza contínua ativado.");
        EmitCleanupLog("=== Bot de Limpeza de Mídias Concluídas ===");
        EmitCleanupLog("MongoDB: configurado (URI ocultada por segurança)");
        EmitCleanupLog("Database: ftp");
        EmitCleanupLog($"Fontes monitoradas: [{sourceListStr}]");
        EmitCleanupLog("Modo: Contínuo (a cada 30s)");
        EmitCleanupLog("Dry-run: False");
        EmitCleanupLog("===========================================");
        EmitCleanupLog(string.Empty);

        _cleanupTask = Task.Run(() => RunContinuousCleanupLoopAsync(sources, _cleanupCts.Token), _cleanupCts.Token);
    }

    private async Task RunContinuousCleanupLoopAsync(List<string> sources, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
                if (_mongoContext != null && !_streamOnly)
                {
                    await RunCleanupCycleAsync(sources, cancellationToken).ConfigureAwait(false);
                }

                // Watchdog: Auto-monta N: apenas se UseMappedDrive=true (compatibilidade legada)
                var cfg = NormalizeRuntimeConfiguration(Config);
                if (cfg.UseMappedDrive && (_isEnvioRunning || _isDownloaderRunning || _telegramPool != null) && !IsDriveNAccessible())
                {
                    var nowTicks = DateTime.UtcNow.Ticks;
                    var nextAttemptTicks = Interlocked.Read(ref _nextAutomaticMountAttemptUtcTicks);
                    if (nowTicks >= nextAttemptTicks
                        && Interlocked.CompareExchange(ref _automaticMountAttemptInProgress, 1, 0) == 0)
                    {
                        _logger.LogWarning("[NEBULA-WATCHDOG] Unidade N: inacessível (UseMappedDrive=true). Iniciando auto-recuperação...");
                        AddServerLog("[NEBULA-WATCHDOG] Unidade N: inacessível. Remontando automaticamente...");
                        _ = Task.Run(
                            async () =>
                            {
                                try
                                {
                                    var mounted = await MountDriveNAsync(CancellationToken.None).ConfigureAwait(false);
                                    if (mounted)
                                    {
                                        Interlocked.Exchange(ref _automaticMountFailures, 0);
                                        Interlocked.Exchange(ref _nextAutomaticMountAttemptUtcTicks, 0);
                                    }
                                    else
                                    {
                                        var failures = Interlocked.Increment(ref _automaticMountFailures);
                                        var delaySeconds = Math.Min(600, 30 * (1 << Math.Min(failures, 4)));
                                        Interlocked.Exchange(
                                            ref _nextAutomaticMountAttemptUtcTicks,
                                            DateTime.UtcNow.AddSeconds(delaySeconds).Ticks);
                                        _logger.LogWarning(
                                            "[NEBULA-WATCHDOG] Auto-recuperação da unidade N: não concluída. Nova tentativa em aproximadamente {DelaySeconds}s.",
                                            delaySeconds);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    var failures = Interlocked.Increment(ref _automaticMountFailures);
                                    var delaySeconds = Math.Min(600, 30 * (1 << Math.Min(failures, 4)));
                                    Interlocked.Exchange(
                                        ref _nextAutomaticMountAttemptUtcTicks,
                                        DateTime.UtcNow.AddSeconds(delaySeconds).Ticks);
                                    _logger.LogError(ex, "[NEBULA-WATCHDOG] Erro ao auto-remontar unidade N:. Nova tentativa em aproximadamente {DelaySeconds}s.", delaySeconds);
                                }
                                finally
                                {
                                    Interlocked.Exchange(ref _automaticMountAttemptInProgress, 0);
                                }
                            },
                            CancellationToken.None);
                    }
                }
                else if (!cfg.UseMappedDrive && !IsDriveNAccessible())
                {
                    // Log apenas em debug para não poluir - N: desmontado é o comportamento esperado
                    _logger.LogDebug("[NEBULA-WATCHDOG] Unidade N: desmontada (UseMappedDrive=false). Streaming via HTTP/FTP direto.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro no ciclo contínuo de monitoramento do Nebula.");
            }
        }
    }

    private async Task RunCleanupCycleAsync(List<string> sources, CancellationToken cancellationToken)
    {
        if (_mongoContext == null)
        {
            return;
        }

        var completedPaths = await _mongoContext.GetCompletedTelegramLocalPathsAsync(cancellationToken).ConfigureAwait(false);
        if (completedPaths.Count == 0)
        {
            return;
        }

        foreach (var source in sources)
        {
            if (!Directory.Exists(source))
            {
                continue;
            }

            try
            {
                var files = Directory.GetFiles(source, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    string fullPath;
                    try
                    {
                        fullPath = Path.GetFullPath(file);
                    }
                    catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
                    {
                        _logger.LogDebug(ex, "Caminho inválido ignorado na limpeza contínua: {File}", file);
                        continue;
                    }

                    if (completedPaths.Contains(fullPath))
                    {
                        var fi = new FileInfo(file);
                        if (fi.Exists)
                        {
                            try
                            {
                                fi.Delete();
                                EmitCleanupLog($"Arquivo removido (já no Telegram): {fullPath}");
                            }
                            catch (Exception delEx)
                            {
                                _logger.LogDebug(delEx, "Erro ao remover arquivo já enviado: {File}", file);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Erro ao varrer pasta fonte de limpeza: {Source}", source);
            }
        }
    }

    private void AddServerLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        ParseServerLine(line);

        lock (_lock)
        {
            _serverLogs.Add(line);
            _serverLogSeq++;
            if (_serverLogs.Count > MaxLogLines)
            {
                _serverLogs.RemoveRange(0, _serverLogs.Count - MaxLogLines);
            }
        }
    }

    private void ParseServerLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        try
        {
            // 1. Iniciando upload: [W4] Iniciando upload: Frankenstein (2025).mkv tamanho=3005.85 MB partes=188 paralelo=1 bots=27
            var mStart = Regex.Match(line, @"\[W(\d+)\]\s+Iniciando upload:\s*(.+?)\s+tamanho=([\d.]+)\s*MB\s+partes=(\d+)", RegexOptions.IgnoreCase);
            if (mStart.Success)
            {
                var wKey = $"W{mStart.Groups[1].Value}";
                var name = mStart.Groups[2].Value.Trim();
                double.TryParse(mStart.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var sizeMb);
                int.TryParse(mStart.Groups[4].Value, out var totalParts);

                _activeUploads[wKey] = new NebulaWorkerItemDto
                {
                    WorkerId = wKey,
                    Name = name,
                    DisplayName = $"[{wKey}] {name}",
                    Size = (long)(sizeMb * 1024 * 1024),
                    UploadedBytes = 0,
                    Percentage = 0,
                    Status = "uploading",
                    BotText = "Bots ativos",
                    InfoText = $"[{wKey}] {name} (0/{totalParts} partes | {sizeMb:F1} MB)"
                };
                return;
            }

            // 2. Retomando upload: [W4] Retomando Frankenstein (2025).mkv na parte 79/188 (1264.00 MB ja enviados)
            var mResume = Regex.Match(line, @"\[W(\d+)\]\s+Retomando\s+(.+?)\s+na parte\s+(\d+)/(\d+)\s*\(([\d.]+)\s*MB ja enviados\)", RegexOptions.IgnoreCase);
            if (mResume.Success)
            {
                var wKey = $"W{mResume.Groups[1].Value}";
                var name = mResume.Groups[2].Value.Trim();
                int.TryParse(mResume.Groups[3].Value, out var curPart);
                int.TryParse(mResume.Groups[4].Value, out var totalParts);
                double.TryParse(mResume.Groups[5].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var sentMb);

                double pct = totalParts > 0 ? ((double)curPart / totalParts) * 100.0 : 0;
                _activeUploads[wKey] = new NebulaWorkerItemDto
                {
                    WorkerId = wKey,
                    Name = name,
                    DisplayName = $"[{wKey}] {name}",
                    Size = (long)(totalParts * 16L * 1024 * 1024),
                    UploadedBytes = (long)(sentMb * 1024 * 1024),
                    Percentage = Math.Round(pct, 1),
                    Status = "uploading",
                    BotText = "Bots ativos",
                    InfoText = $"[{wKey}] {name} ({curPart}/{totalParts} partes | {sentMb:F1} MB)"
                };
                return;
            }

            // 3. Parte iniciando: [UPLOAD] W5 parte=80 bot=#2 iniciando
            var mPartStart = Regex.Match(line, @"\[UPLOAD\]\s+W(\d+)\s+parte=(\d+)\s+bot=#?(\d+)\s+iniciando", RegexOptions.IgnoreCase);
            if (mPartStart.Success)
            {
                var wKey = $"W{mPartStart.Groups[1].Value}";
                int.TryParse(mPartStart.Groups[2].Value, out var partNum);
                int.TryParse(mPartStart.Groups[3].Value, out var botNum);

                if (_activeUploads.TryGetValue(wKey, out var item))
                {
                    item.Status = "uploading";
                    item.BotText = $"Bot #{botNum}";
                    item.InfoText = $"[{wKey}] {item.Name} (Parte {partNum} | Bot #{botNum})";
                }
                return;
            }

            // 4. Parte concluida: [UPLOAD] W5 parte=80 bot=#2 concluida em 4.8s (3.32 MB/s)
            var mPartDone = Regex.Match(line, @"\[UPLOAD\]\s+W(\d+)\s+parte=(\d+)\s+bot=#?(\d+)\s+concluida em ([\d.]+)s\s*\(([\d.]+\s*MB/s)\)", RegexOptions.IgnoreCase);
            if (mPartDone.Success)
            {
                var wKey = $"W{mPartDone.Groups[1].Value}";
                int.TryParse(mPartDone.Groups[2].Value, out var partNum);
                int.TryParse(mPartDone.Groups[3].Value, out var botNum);
                var speed = mPartDone.Groups[5].Value;

                if (_activeUploads.TryGetValue(wKey, out var item))
                {
                    item.UploadedBytes = partNum * 16L * 1024 * 1024;
                    if (item.Size > 0)
                    {
                        item.Percentage = Math.Min(100.0, Math.Round(((double)item.UploadedBytes / item.Size) * 100.0, 1));
                    }
                    item.BotText = $"Bot #{botNum}";
                    item.InfoText = $"[{wKey}] {item.Name} ({item.Percentage:F1}% | {speed} | Bot #{botNum})";
                }
                return;
            }

            // 5. Conclusão: [W5] Upload concluido ou Concluído
            var mFin = Regex.Match(line, @"\[W(\d+)\]\s+(?:Upload conclu[íi]do|Upload finalizado|Conclu[íi]do)", RegexOptions.IgnoreCase);
            if (mFin.Success)
            {
                var wKey = $"W{mFin.Groups[1].Value}";
                if (_activeUploads.TryGetValue(wKey, out var item))
                {
                    item.Percentage = 100;
                    item.Status = "completed";
                    item.InfoText = $"[{wKey}] {item.Name} (100% Concluído)";
                }
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro no parse de linha do servidor Nebula");
        }
    }

    private void AddDownloaderLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var ts = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var formatted = $"[{ts}] {line}";
        ParseDownloaderLine(formatted);

        lock (_lock)
        {
            _downloaderLogs.Add(formatted);
            _downloaderLogSeq++;
            if (_downloaderLogs.Count > MaxLogLines)
            {
                _downloaderLogs.RemoveRange(0, _downloaderLogs.Count - MaxLogLines);
            }
        }
    }

    private void ParseDownloaderLine(string line)
    {
        // 1. Detect start or ready media move
        var mStart = Regex.Match(line, @"Iniciando download:\s*(.+?)(?:\s+\[.+?\])?\s*->\s*Stage:\s*(.+)", RegexOptions.IgnoreCase);
        if (mStart.Success)
        {
            lock (_lock)
            {
                _currentDownload.Name = $"Baixando: {mStart.Groups[1].Value.Trim()}";
                _currentDownload.StageStep = $"Destino em Stage: {mStart.Groups[2].Value.Trim()}";
                _currentDownload.Percentage = 0;
                _currentDownload.DetailText = "0.0% (Iniciando download...)";
            }
            return;
        }

        var mReady = Regex.Match(line, @"M[ií]dia pronta detectada:\s*(.+?)(?:\s+\[.+?\])?\s*->\s*Movendo para Stage:\s*(.+)", RegexOptions.IgnoreCase);
        if (mReady.Success)
        {
            lock (_lock)
            {
                _currentDownload.Name = $"Mídia Pronta: {mReady.Groups[1].Value.Trim()}";
                _currentDownload.StageStep = $"Movendo para Stage: {mReady.Groups[2].Value.Trim()}";
                _currentDownload.Percentage = 50;
                _currentDownload.DetailText = "50.0% (Transferindo para Stage...)";
            }
            return;
        }

        // 2. Progress match
        var mProg = Regex.Match(line, @"Baixando\s+(.+?):\s*([\d.]+)\s*MB\s*/\s*([\d.]+)\s*MB\s*\((\d+)%\)(?:\s*-\s*Vel:\s*([\d.]+\s*MB/s))?", RegexOptions.IgnoreCase);
        if (mProg.Success)
        {
            var name = mProg.Groups[1].Value.Trim();
            double.TryParse(mProg.Groups[2].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doneMb);
            double.TryParse(mProg.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var totalMb);
            double.TryParse(mProg.Groups[4].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var pct);
            var vel = mProg.Groups[5].Success ? mProg.Groups[5].Value : string.Empty;

            lock (_lock)
            {
                _currentDownload.Name = $"Baixando: {name}";
                _currentDownload.Percentage = pct;
                _currentDownload.DoneMb = doneMb;
                _currentDownload.TotalMb = totalMb;
                _currentDownload.Speed = vel;
                var velStr = !string.IsNullOrEmpty(vel) ? $" | Vel: {vel}" : string.Empty;
                _currentDownload.DetailText = $"{pct:F1}% ({doneMb:F1} MB / {totalMb:F1} MB{velStr})";
            }
            return;
        }

        // 3. Merging parts
        var mMerge = Regex.Match(line, @"Unindo partes do arquivo:\s*(.+)", RegexOptions.IgnoreCase);
        if (mMerge.Success)
        {
            lock (_lock)
            {
                _currentDownload.Name = $"Processando: {mMerge.Groups[1].Value.Trim()}";
                _currentDownload.StageStep = "Download concluído. Unindo partes do arquivo...";
                _currentDownload.Percentage = 99;
                _currentDownload.DetailText = "99.0% (Montando arquivo final...)";
            }
            return;
        }

        // 4. Enqueued into MongoDB
        var mQueue = Regex.Match(line, @"Enfileirado no Nebula com sucesso:\s*(.+?)\s*->\s*(.+?)\s*\(tamanho=([\d.]+)\s*MB", RegexOptions.IgnoreCase);
        if (mQueue.Success)
        {
            lock (_lock)
            {
                _currentDownload.Name = $"Enfileirado: {mQueue.Groups[1].Value.Trim()}";
                _currentDownload.StageStep = $"Registrado na fila: {mQueue.Groups[2].Value.Trim()} ({mQueue.Groups[3].Value} MB)";
                _currentDownload.Percentage = 100;
                _currentDownload.DetailText = "100.0% (Enfileirado para envio)";
            }
            return;
        }

        // 5. Completion
        var mDone = Regex.Match(line, @"(?:Conclus[aã]o|Conclu[íi]do)\s+(?:do\s+)?processamento de:\s*(.+?)(?:\.\s*Pronto|\.$|$)", RegexOptions.IgnoreCase);
        if (mDone.Success)
        {
            lock (_lock)
            {
                _currentDownload.Name = $"Concluído: {mDone.Groups[1].Value.Trim()}";
                _currentDownload.StageStep = "Mídia enfileirada no Nebula e .strm deletado. Pronto para próxima mídia.";
                _currentDownload.Percentage = 100;
                _currentDownload.DetailText = "100.0% (Aguardando próxima mídia...)";
            }
            return;
        }

        if (line.Contains("Espaço insuficiente no stage", StringComparison.OrdinalIgnoreCase) || line.Contains("Backpressure", StringComparison.OrdinalIgnoreCase))
        {
            lock (_lock)
            {
                _currentDownload.StageStep = "⏸ Pausado: aguardando liberação de espaço em disco pelo Nebula...";
            }
        }
    }

    public List<NebulaBotDto> GetBots()
    {
        var config = Config;
        var tokens = (config.BotTokens ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var sessionsDir = GetSessionsDirectory();
        var result = new List<NebulaBotDto>();

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var botIndex = i + 1;
            var sessionName = $"Nebula_Bot_{botIndex}.session";
            var sessionPath = Path.Combine(sessionsDir, sessionName);
            var sessionExists = File.Exists(sessionPath);

            var masked = MaskBotToken(token);

            result.Add(new NebulaBotDto
            {
                Index = botIndex,
                Name = $"Nebula_Bot_{botIndex}",
                Token = string.Empty,
                MaskedToken = masked,
                SessionExists = sessionExists && new FileInfo(sessionPath).Length > 0,
                Enabled = true
            });
        }

        return result;
    }

    internal static string MaskBotToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "••••";
        }

        return token.Length > 4
            ? $"••••{token[^4..]}"
            : "••••";
    }

    public List<NebulaBotDto> SaveBot(NebulaSaveBotRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token))
        {
            return GetBots();
        }

        var config = Config;
        var tokens = (config.BotTokens ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var tokenToSave = request.Token.Trim();

        if (request.Index.HasValue && request.Index.Value >= 1 && request.Index.Value <= tokens.Count)
        {
            tokens[request.Index.Value - 1] = tokenToSave;
        }
        else
        {
            if (!tokens.Contains(tokenToSave, StringComparer.OrdinalIgnoreCase))
            {
                tokens.Add(tokenToSave);
            }
        }

        config.BotTokens = string.Join(",", tokens);
        _configManager.SaveConfiguration("nebulaftp", config);

        return GetBots();
    }

    public List<NebulaBotDto> DeleteBot(int index)
    {
        var config = Config;
        var tokens = (config.BotTokens ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (index >= 1 && index <= tokens.Count)
        {
            tokens.RemoveAt(index - 1);
            config.BotTokens = string.Join(",", tokens);
            _configManager.SaveConfiguration("nebulaftp", config);
        }

        return GetBots();
    }

    public List<NebulaBotDto> SyncBotsFromEnv()
    {
        var config = ImportDotEnvConfiguration(Config, save: true);
        return GetBots();
    }

    public async Task<NebulaSupabaseTestResponseDto> TestSupabaseConnectionAsync(NebulaSupabaseTestRequest? request, CancellationToken cancellationToken = default)
    {
        var url = request?.Url ?? Config.SupabaseUrl;
        var key = request?.Key ?? Config.SupabaseKey;

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
        {
            return new NebulaSupabaseTestResponseDto
            {
                Success = false,
                Message = "Supabase URL e API Key são obrigatórios.",
                StatusCode = 400
            };
        }

        if (!IsSafeSupabaseUrl(url, out var safeUrl))
        {
            return new NebulaSupabaseTestResponseDto
            {
                Success = false,
                Message = "A URL do Supabase deve ser HTTPS, absoluta e não pode conter credenciais embutidas.",
                StatusCode = 400
            };
        }

        try
        {
            using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            var endpoint = safeUrl.TrimEnd('/') + "/rest/v1/";
            using var msg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, endpoint);
            msg.Headers.Add("apikey", key);
            msg.Headers.Add("Authorization", $"Bearer {key}");
            msg.Headers.Add("Accept", "application/json");
            msg.Headers.Add("User-Agent", "MulletaFlix-Backend/1.0");

            using var resp = await httpClient.SendAsync(msg, cancellationToken).ConfigureAwait(false);
            var statusInt = (int)resp.StatusCode;

            if (resp.IsSuccessStatusCode || statusInt == 404 || statusInt == 200)
            {
                AddServerLog("[SUPABASE] Teste de conexão com o Supabase realizado com sucesso!");
                return new NebulaSupabaseTestResponseDto
                {
                    Success = true,
                    Message = $"Conectado com sucesso ao Supabase! (HTTP {statusInt})",
                    StatusCode = statusInt
                };
            }

            var errBody = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (errBody.Length > 4096)
            {
                errBody = errBody[..4096] + "…";
            }
            AddServerLog($"[SUPABASE-ERRO] Falha no teste de conexão: HTTP {statusInt} - {errBody}");
            return new NebulaSupabaseTestResponseDto
            {
                Success = false,
                Message = $"Falha na autenticação do Supabase (HTTP {statusInt}): {errBody}",
                StatusCode = statusInt
            };
        }
        catch (Exception ex)
        {
            AddServerLog($"[SUPABASE-ERRO] Erro ao testar conexão com Supabase: {ex.Message}");
            return new NebulaSupabaseTestResponseDto
            {
                Success = false,
                Message = $"Erro de conexão: {ex.Message}",
                StatusCode = 500
            };
        }
    }

    internal static bool IsSafeSupabaseUrl(string? url, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        normalizedUrl = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return true;
    }

    public async Task<NebulaSupabaseStatusDto> GetSupabaseStatusAsync(CancellationToken cancellationToken = default)
    {
        var config = Config;
        var hasUrl = !string.IsNullOrWhiteSpace(config.SupabaseUrl);
        var hasKey = !string.IsNullOrWhiteSpace(config.SupabaseKey);

        var dto = new NebulaSupabaseStatusDto
        {
            IsConfigured = hasUrl && hasKey,
            SupabaseUrl = config.SupabaseUrl,
            HasKey = hasKey,
            AutoBackupEnabled = config.SupabaseAutoBackup,
            AutoBackupIntervalHours = config.SupabaseAutoBackupIntervalHours,
            LastBackupTime = config.SupabaseLastBackupTime,
            LastBackupStatus = config.SupabaseLastBackupStatus,
            TotalLocalFiles = 0,
            TotalRemoteFiles = config.SupabaseLastBackupFilesCount,
            Message = hasUrl ? "Configurado" : "Supabase não configurado"
        };

        if (dto.IsConfigured)
        {
            try
            {
                var test = await TestSupabaseConnectionAsync(new NebulaSupabaseTestRequest { Url = config.SupabaseUrl, Key = config.SupabaseKey }, cancellationToken).ConfigureAwait(false);
                dto.IsConnected = test.Success;
                if (!test.Success)
                {
                    dto.Message = test.Message;
                }
            }
            catch
            {
                dto.IsConnected = false;
                dto.Message = "Erro de conexão ao testar status";
            }
        }

        return dto;
    }

    public async Task<NebulaSupabaseBackupResultDto> BackupMongoToSupabaseAsync(string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        if (await TryGetOperationReplayAsync<NebulaSupabaseBackupResultDto>("supabase-backup", idempotencyKey, cancellationToken).ConfigureAwait(false))
        {
            TryGetOperationReplay("supabase-backup", idempotencyKey, out NebulaSupabaseBackupResultDto? cached);
            return cached!;
        }

        var config = Config;
        if (string.IsNullOrWhiteSpace(config.SupabaseUrl) || string.IsNullOrWhiteSpace(config.SupabaseKey))
        {
            return new NebulaSupabaseBackupResultDto
            {
                Success = false,
                Message = "Configure a URL e a API Key do Supabase antes de iniciar o backup."
            };
        }

        AddServerLog("[SUPABASE-BACKUP] Iniciando sincronização do MongoDB para o Supabase (Nativo C#)...");
        await _maintenanceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (await TryGetOperationReplayAsync<NebulaSupabaseBackupResultDto>("supabase-backup", idempotencyKey, cancellationToken).ConfigureAwait(false))
        {
            TryGetOperationReplay("supabase-backup", idempotencyKey, out NebulaSupabaseBackupResultDto? cached);
            _maintenanceLock.Release();
            return cached!;
        }
        BeginMaintenanceOperation("supabase-backup");
        var operationTimer = Stopwatch.StartNew();
        _logger.LogInformation("[NEBULA-OPERATION] Supabase backup started.");
        var sharedRuntimeLockAcquired = false;
        try
        {
            await _sharedRuntimeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            sharedRuntimeLockAcquired = true;
        }
        catch
        {
            CompleteMaintenanceOperation("failed", "Não foi possível adquirir o lock do runtime compartilhado.");
            _maintenanceLock.Release();
            throw;
        }

        NebulaMongoContext? tempMongo = null;
        NebulaSupabaseSyncService? tempSyncService = null;
        try
        {
            var syncService = _supabaseSyncService;
            if (syncService == null)
            {
                var mongo = _mongoContext;
                if (mongo == null && !string.IsNullOrWhiteSpace(config.MongoDbConnectionString))
                {
                    tempMongo = new NebulaMongoContext(config.MongoDbConnectionString, "ftp", _loggerFactory.CreateLogger<NebulaMongoContext>());
                    mongo = tempMongo;
                }

                if (mongo != null)
                {
                    tempSyncService = new NebulaSupabaseSyncService(mongo, _loggerFactory.CreateLogger<NebulaSupabaseSyncService>(), _usersDbProvider);
                    syncService = tempSyncService;
                }
            }

            if (syncService == null)
            {
                AddServerLog("[SUPABASE-ERRO] Contexto do MongoDB ou Supabase não pôde ser instanciado.");
                CompleteMaintenanceOperation("failed", "Contexto do MongoDB ou Supabase não pôde ser instanciado.");
                return new NebulaSupabaseBackupResultDto
                {
                    Success = false,
                    Message = "Contexto do MongoDB ou Supabase não pôde ser instanciado.",
                    Timestamp = DateTime.UtcNow
                };
            }

            void ReportBackupProgress(string message)
            {
                AddServerLog(message);
                UpdateMaintenanceOperationProgress(message);
            }

            var result = await syncService.PerformBackupAsync(config.SupabaseUrl, config.SupabaseKey, ReportBackupProgress, cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                AddServerLog($"[SUPABASE] {result.Message}");
                CompleteMaintenanceOperation("succeeded");
                config.SupabaseLastBackupTime = DateTime.UtcNow;
                config.SupabaseLastBackupStatus = $"Backup realizado com sucesso ({result.FilesBackedUp} arquivos) em {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            }
            else
            {
                AddServerLog($"[SUPABASE-ERRO] Falha na sincronização: {result.Message}");
                CompleteMaintenanceOperation("failed", result.Message);
                config.SupabaseLastBackupStatus = $"Falha no backup às {DateTime.Now:dd/MM/yyyy HH:mm:ss}: {result.Message}";
            }

            _configManager.SaveConfiguration("nebulaftp", config);
            await CacheOperationReplayAsync("supabase-backup", idempotencyKey, result, CancellationToken.None).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AddServerLog("[SUPABASE-BACKUP] Backup cancelado.");
            CompleteMaintenanceOperation("cancelled");
            throw;
        }
        catch (Exception ex)
        {
            AddServerLog($"[SUPABASE-ERRO] Exceção durante o backup nativo: {ex.Message}");
            _logger.LogError(ex, "Erro durante o backup nativo para Supabase.");
            CompleteMaintenanceOperation("failed", ex.Message);
            var failedResult = new NebulaSupabaseBackupResultDto
            {
                Success = false,
                Message = $"Erro durante o backup: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
            await CacheOperationReplayAsync("supabase-backup", idempotencyKey, failedResult, CancellationToken.None).ConfigureAwait(false);
            return failedResult;
        }
        finally
        {
            operationTimer.Stop();
            _logger.LogInformation(
                "[NEBULA-OPERATION] Supabase backup finished in {DurationMs} ms.",
                operationTimer.ElapsedMilliseconds);
            tempSyncService?.Dispose();
            tempMongo?.Dispose();
            if (sharedRuntimeLockAcquired)
            {
                _sharedRuntimeLock.Release();
            }
            _maintenanceLock.Release();
        }
    }

    public async Task<NebulaSupabaseRestoreResultDto> RestoreSupabaseToMongoAsync(string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        if (await TryGetOperationReplayAsync<NebulaSupabaseRestoreResultDto>("supabase-restore", idempotencyKey, cancellationToken).ConfigureAwait(false))
        {
            TryGetOperationReplay("supabase-restore", idempotencyKey, out NebulaSupabaseRestoreResultDto? cached);
            return cached!;
        }

        var config = NormalizeRuntimeConfiguration(Config);
        if (string.IsNullOrWhiteSpace(config.SupabaseUrl) || string.IsNullOrWhiteSpace(config.SupabaseKey))
        {
            return new NebulaSupabaseRestoreResultDto
            {
                Success = false,
                Message = "Configure a URL e a API Key do Supabase antes de iniciar a restauração."
            };
        }

        AddServerLog("[SUPABASE-RESTORE] Iniciando restauração do acervo a partir do Supabase (Nativo C#)...");
        await _maintenanceLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (await TryGetOperationReplayAsync<NebulaSupabaseRestoreResultDto>("supabase-restore", idempotencyKey, cancellationToken).ConfigureAwait(false))
        {
            TryGetOperationReplay("supabase-restore", idempotencyKey, out NebulaSupabaseRestoreResultDto? cached);
            _maintenanceLock.Release();
            return cached!;
        }
        BeginMaintenanceOperation("supabase-restore");
        var operationTimer = Stopwatch.StartNew();
        _logger.LogInformation("[NEBULA-OPERATION] Supabase restore started.");
        var sharedRuntimeLockAcquired = false;
        try
        {
            await _sharedRuntimeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            sharedRuntimeLockAcquired = true;
        }
        catch
        {
            CompleteMaintenanceOperation("failed", "Não foi possível adquirir o lock do runtime compartilhado.");
            _maintenanceLock.Release();
            throw;
        }

        NebulaMongoContext? tempMongo = null;
        NebulaSupabaseSyncService? tempSyncService = null;
        try
        {
            var syncService = _supabaseSyncService;
            if (syncService == null)
            {
                var mongo = _mongoContext;
                if (mongo == null && !string.IsNullOrWhiteSpace(config.MongoDbConnectionString))
                {
                    tempMongo = new NebulaMongoContext(config.MongoDbConnectionString, "ftp", _loggerFactory.CreateLogger<NebulaMongoContext>());
                    mongo = tempMongo;
                }

                if (mongo != null)
                {
                    tempSyncService = new NebulaSupabaseSyncService(mongo, _loggerFactory.CreateLogger<NebulaSupabaseSyncService>(), _usersDbProvider);
                    syncService = tempSyncService;
                }
            }

            if (syncService == null)
            {
                AddServerLog("[SUPABASE-RESTORE-ERRO] Contexto do MongoDB ou Supabase não pôde ser instanciado.");
                CompleteMaintenanceOperation("failed", "Contexto do MongoDB ou Supabase não pôde ser instanciado.");
                return new NebulaSupabaseRestoreResultDto
                {
                    Success = false,
                    Message = "Contexto do MongoDB ou Supabase não pôde ser instanciado.",
                    Timestamp = DateTime.UtcNow
                };
            }

            void ReportRestoreProgress(string message)
            {
                AddServerLog(message);
                UpdateMaintenanceOperationProgress(message);
            }

            var result = await syncService.PerformRestoreAsync(config.SupabaseUrl, config.SupabaseKey, ReportRestoreProgress, cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                AddServerLog($"[SUPABASE-RESTORE] {result.Message}");
                CompleteMaintenanceOperation("succeeded");
            }
            else
            {
                AddServerLog($"[SUPABASE-RESTORE-ERRO] Falha na restauração: {result.Message}");
                CompleteMaintenanceOperation("failed", result.Message);
            }

            await CacheOperationReplayAsync("supabase-restore", idempotencyKey, result, CancellationToken.None).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AddServerLog("[SUPABASE-RESTORE] Restauração cancelada.");
            CompleteMaintenanceOperation("cancelled");
            throw;
        }
        catch (Exception ex)
        {
            AddServerLog($"[SUPABASE-RESTORE-ERRO] Exceção durante a restauração nativa: {ex.Message}");
            _logger.LogError(ex, "Erro durante a restauração nativa do Supabase.");
            CompleteMaintenanceOperation("failed", ex.Message);
            var failedResult = new NebulaSupabaseRestoreResultDto
            {
                Success = false,
                Message = $"Erro durante a restauração: {ex.Message}",
                Timestamp = DateTime.UtcNow
            };
            await CacheOperationReplayAsync("supabase-restore", idempotencyKey, failedResult, CancellationToken.None).ConfigureAwait(false);
            return failedResult;
        }
        finally
        {
            operationTimer.Stop();
            _logger.LogInformation(
                "[NEBULA-OPERATION] Supabase restore finished in {DurationMs} ms.",
                operationTimer.ElapsedMilliseconds);
            tempSyncService?.Dispose();
            tempMongo?.Dispose();
            if (sharedRuntimeLockAcquired)
            {
                _sharedRuntimeLock.Release();
            }
            _maintenanceLock.Release();
        }
    }

    public string GetSupabaseSqlScript()
    {
        return @"-- =========================================================================
-- SCRIPT DE CRIAÇÃO DE TABELAS PARA BACKUP DO NEBULA NO SUPABASE
-- Execute este script no SQL Editor do seu Dashboard do Supabase
-- =========================================================================

CREATE TABLE IF NOT EXISTS nebula_files (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    parent TEXT,
    size BIGINT DEFAULT 0,
    status TEXT NOT NULL,
    parts JSONB,
    uploaded_at NUMERIC,
    doc_data JSONB NOT NULL,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_nebula_files_status ON nebula_files(status);
CREATE INDEX IF NOT EXISTS idx_nebula_files_parent ON nebula_files(parent);
CREATE INDEX IF NOT EXISTS idx_nebula_files_name ON nebula_files(name);

CREATE TABLE IF NOT EXISTS nebula_bot_tokens (
    id BIGSERIAL PRIMARY KEY,
    index INTEGER NOT NULL,
    token TEXT NOT NULL,
    enabled BOOLEAN NOT NULL DEFAULT TRUE,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_nebula_bot_tokens_index ON nebula_bot_tokens(index);

CREATE TABLE IF NOT EXISTS nebula_users (
    login TEXT PRIMARY KEY,
    password_hash TEXT,
    permissions JSONB,
    doc_data JSONB NOT NULL,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Usuários de login do MulletaFlix/Jellyfin. A senha é armazenada somente
-- como o hash já existente no banco local; nunca é enviada em texto puro.
CREATE TABLE IF NOT EXISTS mulletaflix_users (
    id UUID PRIMARY KEY,
    username TEXT NOT NULL,
    normalized_username TEXT NOT NULL,
    password TEXT,
    phone_number TEXT,
    must_update_password BOOLEAN NOT NULL DEFAULT FALSE,
    authentication_provider_id TEXT NOT NULL,
    password_reset_provider_id TEXT NOT NULL,
    enable_local_password BOOLEAN NOT NULL DEFAULT TRUE,
    enable_user_preference_access BOOLEAN NOT NULL DEFAULT TRUE,
    permissions JSONB NOT NULL DEFAULT '[]'::jsonb,
    license JSONB,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_mulletaflix_users_username ON mulletaflix_users(normalized_username);
ALTER TABLE mulletaflix_users ADD COLUMN IF NOT EXISTS license JSONB;

CREATE TABLE IF NOT EXISTS nebula_backups (
    id BIGSERIAL PRIMARY KEY,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    backup_type TEXT DEFAULT 'manual',
    status TEXT DEFAULT 'success',
    total_files INTEGER DEFAULT 0,
    total_users INTEGER DEFAULT 0,
    details TEXT
);

-- Habilita RLS em todas as tabelas.
-- A chave 'service_role' do Supabase ignora RLS por design e é a única usada
-- pelo backend. As políticas abaixo documentam isso explicitamente e bloqueiam
-- acesso anônimo/autenticado direto ao banco.
ALTER TABLE nebula_files ENABLE ROW LEVEL SECURITY;
ALTER TABLE nebula_users ENABLE ROW LEVEL SECURITY;
ALTER TABLE mulletaflix_users ENABLE ROW LEVEL SECURITY;
ALTER TABLE nebula_backups ENABLE ROW LEVEL SECURITY;
ALTER TABLE nebula_bot_tokens ENABLE ROW LEVEL SECURITY;

-- Remove políticas anteriores (idempotência ao re-executar o script)
DROP POLICY IF EXISTS nebula_files_service_role_all ON nebula_files;
DROP POLICY IF EXISTS nebula_users_service_role_all ON nebula_users;
DROP POLICY IF EXISTS mulletaflix_users_service_role_all ON mulletaflix_users;
DROP POLICY IF EXISTS nebula_backups_service_role_all ON nebula_backups;
DROP POLICY IF EXISTS nebula_bot_tokens_service_role_all ON nebula_bot_tokens;

-- Cria políticas permissivas apenas para a role 'service_role' (backend).
-- Roles 'anon' e 'authenticated' não possuem políticas e são bloqueadas por padrão.
CREATE POLICY nebula_files_service_role_all
    ON nebula_files FOR ALL TO service_role USING (true) WITH CHECK (true);

CREATE POLICY nebula_users_service_role_all
    ON nebula_users FOR ALL TO service_role USING (true) WITH CHECK (true);

CREATE POLICY mulletaflix_users_service_role_all
    ON mulletaflix_users FOR ALL TO service_role USING (true) WITH CHECK (true);

CREATE POLICY nebula_backups_service_role_all
    ON nebula_backups FOR ALL TO service_role USING (true) WITH CHECK (true);

CREATE POLICY nebula_bot_tokens_service_role_all
    ON nebula_bot_tokens FOR ALL TO service_role USING (true) WITH CHECK (true);
";
    }

    /// <summary>
    /// Obtém o pool de clientes Telegram MTProto e Bot API em execução.
    /// </summary>
    public NebulaTelegramPool? TelegramPool => _telegramPool;

    /// <inheritdoc />
    public async Task<bool> SendTelegramNotificationAsync(string messageHtml, string? targetChatId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageHtml))
        {
            return false;
        }

        var pool = _telegramPool;
        if (pool == null)
        {
            var config = Config;
            var botTokens = await LoadBotTokensAsync(config, cancellationToken).ConfigureAwait(false);
            if (botTokens.Length == 0)
            {
                return false;
            }

            _ = int.TryParse(config.ApiId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var apiId);
            _ = long.TryParse(config.ChatId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var chatId);
            await using var tempPool = new NebulaTelegramPool(apiId, config.ApiHash, botTokens, chatId, GetSessionsDirectory(), _loggerFactory.CreateLogger<NebulaTelegramPool>());
            return await tempPool.SendMessageAsync(messageHtml, targetChatId, cancellationToken).ConfigureAwait(false);
        }

        return await pool.SendMessageAsync(messageHtml, targetChatId, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        try
        {
            StopOwnedRcloneProcess();

            if (_supabaseSyncService != null)
            {
                _supabaseSyncService.Dispose();
                _supabaseSyncService = null;
            }

            if (_stagingWatcher != null)
            {
                _stagingWatcher.Dispose();
                _stagingWatcher = null;
            }

            if (_ftpServerHost != null)
            {
                _ftpServerHost.Dispose();
                _ftpServerHost = null;
            }

            if (_httpStreamServer != null)
            {
                _httpStreamServer.Dispose();
                _httpStreamServer = null;
            }

            if (_uploadEngine != null)
            {
                _uploadEngine.Dispose();
                _uploadEngine = null;
            }

            if (_downloaderEngine != null)
            {
                _downloaderEngine.Dispose();
                _downloaderEngine = null;
            }

            if (_telegramPool != null)
            {
                _telegramPool.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _telegramPool = null;
            }

            if (_mongoContext != null)
            {
                _mongoContext.Dispose();
                _mongoContext = null;
            }

            _envioLock.Dispose();
            _downloaderLock.Dispose();
            _sharedRuntimeLock.Dispose();
            _maintenanceLock.Dispose();
            _mountLock.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro no Dispose do NebulaFtpManager");
        }
    }

    private static NebulaFtpConfiguration NormalizeRuntimeConfiguration(NebulaFtpConfiguration config)
    {
        config.ServerPort = config.ServerPort is >= 1 and <= 65535 ? config.ServerPort : 2121;
        config.HttpStreamPort = config.HttpStreamPort is >= 1 and <= 65535 ? config.HttpStreamPort : 2123;
        config.MaxActiveConnections = config.MaxActiveConnections is >= 1 and <= 4096 ? config.MaxActiveConnections : 32;
        if (string.IsNullOrWhiteSpace(config.HttpStreamToken))
        {
            config.HttpStreamToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        }
        config.MaxWorkers = Math.Clamp(config.MaxWorkers, 1, 64);
        config.ChunkSizeMb = Math.Clamp(config.ChunkSizeMb, 1, 512);
        config.DownloadParts = Math.Clamp(config.DownloadParts, 1, 32);
        config.SupabaseAutoBackupIntervalHours = Math.Clamp(config.SupabaseAutoBackupIntervalHours, 1, 168);
        return config;
    }

    private static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return System.Net.IPAddress.TryParse(host, out var address) && System.Net.IPAddress.IsLoopback(address);
    }

    private async Task<string[]> LoadBotTokensAsync(NebulaFtpConfiguration config, CancellationToken cancellationToken)
    {
        var fallback = (config.BotTokens ?? string.Empty)
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (_mongoContext == null)
        {
            return fallback;
        }

        var stored = await _mongoContext.GetBotTokensAsync(config.BotTokensCollection, cancellationToken).ConfigureAwait(false);
        if (stored.Count > 0)
        {
            AddServerLog($"[NEBULA-MONGO] {stored.Count} tokens carregados do MongoDB local.");
            return stored.ToArray();
        }

        if (!string.IsNullOrWhiteSpace(config.SupabaseUrl) && !string.IsNullOrWhiteSpace(config.SupabaseKey))
        {
            _supabaseSyncService ??= new NebulaSupabaseSyncService(_mongoContext, _loggerFactory.CreateLogger<NebulaSupabaseSyncService>(), _usersDbProvider);
            var remote = await _supabaseSyncService.GetBotTokensAsync(config.SupabaseUrl, config.SupabaseKey, config.BotTokensTable, cancellationToken).ConfigureAwait(false);
            if (remote.Count > 0)
            {
                AddServerLog($"[NEBULA-SUPABASE] {remote.Count} tokens carregados do Supabase e salvos no MongoDB local.");
                for (int i = 0; i < remote.Count; i++)
                {
                    var tokenDoc = new MongoDB.Bson.BsonDocument
                    {
                        { "index", i + 1 },
                        { "token", remote[i] },
                        { "enabled", true },
                        { "created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                    };
                    await _mongoContext.UpsertRawTokenDocAsync(tokenDoc, cancellationToken).ConfigureAwait(false);
                }

                return remote.ToArray();
            }
        }

        AddServerLog("[NEBULA-MONGO] Coleção de tokens vazia ou indisponível; usando fallback da configuração local.");
        return fallback;
    }

    /// <summary>
    /// Verifica se os dados essenciais locais estão vazios e restaura o backup do Supabase antes de continuar.
    /// A coleção de usuários é verificada separadamente porque uma reinstalação pode manter
    /// registros de mídia, mas perder as contas locais.
    /// </summary>
    private async Task EnsureDatabaseRestoredIfEmptyAsync(
        NebulaFtpConfiguration config,
        Action<string>? logAction,
        CancellationToken cancellationToken)
    {
        if (_mongoContext == null || string.IsNullOrWhiteSpace(config.SupabaseUrl) || string.IsNullOrWhiteSpace(config.SupabaseKey))
        {
            return;
        }

        try
        {
            var fileCount = await _mongoContext.CountFilesAsync(cancellationToken).ConfigureAwait(false);
            var userCount = await _mongoContext.CountUsersAsync(cancellationToken).ConfigureAwait(false);
            var appUserCount = _supabaseSyncService == null
                ? 0
                : await _supabaseSyncService.GetMulletaFlixUserCountAsync(cancellationToken).ConfigureAwait(false);
            var appUserBackupCount = _supabaseSyncService == null
                ? 0
                : await _supabaseSyncService.GetMulletaFlixUserBackupCountAsync(config.SupabaseUrl, config.SupabaseKey, cancellationToken).ConfigureAwait(false);
            if (fileCount == 0 || userCount == 0 || appUserCount == 0 || appUserBackupCount > appUserCount)
            {
                logAction?.Invoke($"[DATABASE-INIT] Dados locais incompletos detectados (arquivos: {fileCount}, usuários FTP: {userCount}, usuários MulletaFlix: {appUserCount}/{appUserBackupCount} no backup). Verificando Supabase...");
                _logger.LogInformation("[DATABASE-INIT] Dados locais incompletos detectados (arquivos: {Files}, usuários FTP: {FtpUsers}, usuários MulletaFlix: {AppUsers}/{BackupUsers}). Iniciando auto-restauração a partir do Supabase ({Url})...", fileCount, userCount, appUserCount, appUserBackupCount, config.SupabaseUrl);

                _supabaseSyncService ??= new NebulaSupabaseSyncService(_mongoContext, _loggerFactory.CreateLogger<NebulaSupabaseSyncService>(), _usersDbProvider);
                var restoreResult = await _supabaseSyncService.PerformRestoreAsync(config.SupabaseUrl, config.SupabaseKey, cancellationToken).ConfigureAwait(false);

                if (restoreResult.Success && (restoreResult.FilesRestored > 0 || restoreResult.UsersRestored > 0))
                {
                    logAction?.Invoke($"[DATABASE-INIT] Auto-restauração concluída! {restoreResult.FilesRestored} arquivos e {restoreResult.UsersRestored} usuários recuperados do Supabase.");
                    _logger.LogInformation("[DATABASE-INIT] Auto-restauração concluída: {Files} arquivos e {Users} usuários restaurados.", restoreResult.FilesRestored, restoreResult.UsersRestored);
                }
                else if (restoreResult.Success)
                {
                    logAction?.Invoke("[DATABASE-INIT] Supabase também está vazio. Inicializando banco de dados novo.");
                }
                else
                {
                    logAction?.Invoke($"[DATABASE-INIT-AVISO] Não foi possível restaurar do Supabase: {restoreResult.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DATABASE-INIT] Erro ao verificar ou restaurar base de dados vazia do Supabase.");
            logAction?.Invoke($"[DATABASE-INIT-ERRO] Erro na auto-restauração: {ex.Message}");
        }
    }

    public async Task<bool> MountDriveNAsync(CancellationToken cancellationToken = default)
    {
        var config = NormalizeRuntimeConfiguration(Config);
        if (!config.UseMappedDrive)
        {
            AddServerLog($"[NEBULA-MOUNT] UseMappedDrive=false: montagem da unidade N: desativada. Use HTTP ({config.HttpStreamPort}) ou FTP ({config.ServerPort}) direto.");
            return false;
        }

        if ((_ftpServerHost == null || !_ftpServerHost.IsRunning) && !_isEnvioRunning)
        {
            AddServerLog("[NEBULA-MOUNT] Servidor FTP não está ativo. Inicializando modo Streaming para montar a unidade N:...");
            var started = await StartEnvioAsync(streamOnly: true, cancellationToken).ConfigureAwait(false);
            if (!started)
            {
                AddServerLog("[NEBULA-MOUNT-ERRO] Falha ao iniciar servidor FTP para montar a unidade N:.");
                return false;
            }
        }

        await _mountLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_rcloneProcess != null && !_rcloneProcess.HasExited && IsDriveNAccessible())
            {
                AddServerLog("[NEBULA-MOUNT] Unidade N: já está montada e acessível.");
                return true;
            }

            // The Python helper supervises rclone and can remain alive while
            // rclone retries a failed mount. Do not kill that owner and start
            // another mount: WinFsp rejects the competing filesystem with
            // ERROR_FILE_EXISTS (80070050).
            if (_rcloneProcess != null && !_rcloneProcess.HasExited)
            {
                AddServerLog("[NEBULA-MOUNT] Montagem N: já está em andamento; aguardando o helper existente.");
                return false;
            }

            var rcloneExe = FindRcloneExe();
            if (string.IsNullOrWhiteSpace(rcloneExe))
            {
                AddServerLog("[NEBULA-MOUNT-ERRO] rclone.exe não foi encontrado no sistema. Verifique a instalação do rclone.");
                return false;
            }

            // Garante liberação de processos ou pontos de montagem prévios antes de montar
            AddServerLog("[NEBULA-MOUNT] Preparando montagem e liberando eventuais instâncias anteriores do rclone...");
            // Clear stale rclone processes from previous Nebula/helper runs
            // before claiming the N: WinFsp drive letter.
            StopAllRcloneProcesses(includeForeignProcesses: true);
            for (var i = 0; i < 6; i++)
            {
                if (!Directory.Exists("N:\\"))
                {
                    break;
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            // 1. Aguarda FTP local responder
            AddServerLog($"[NEBULA-MOUNT] Aguardando servidor FTP 127.0.0.1:{config.ServerPort} responder...");
            var ftpReady = false;
            for (var i = 0; i < 30; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    using var tcp = new System.Net.Sockets.TcpClient();
                    var connectTask = tcp.ConnectAsync("127.0.0.1", config.ServerPort);
                    var completed = await Task.WhenAny(connectTask, Task.Delay(1000, cancellationToken)).ConfigureAwait(false);
                    if (completed == connectTask && tcp.Connected)
                    {
                        ftpReady = true;
                        tcp.Close();
                        break;
                    }
                }
                catch
                {
                    // retry
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            if (!ftpReady)
            {
                AddServerLog($"[NEBULA-MOUNT-AVISO] Servidor FTP não respondeu na porta {config.ServerPort} em 15s. Tentando montar N: mesmo assim...");
            }

            var (ftpUsername, ftpPassword) = EnsureLocalFtpCredentials(config);
            var configPath = EnsureRcloneConfigFile(config.ServerPort, ftpUsername, ftpPassword, rcloneExe);
            var hasInlineCredentials = !string.IsNullOrWhiteSpace(ftpUsername) && !string.IsNullOrWhiteSpace(ftpPassword);

            // 2. Garante usuário configurado no MongoDB para o rclone
            if (hasInlineCredentials && _mongoContext != null)
            {
                try
                {
                    var hash = BCrypt.Net.BCrypt.HashPassword(ftpPassword);
                    await _mongoContext.UpsertUserAsync(ftpUsername, hash, "elradfmwM", cancellationToken).ConfigureAwait(false);
                    AddServerLog($"[NEBULA-MOUNT] Credencial FTP local '{ftpUsername}' sincronizada no MongoDB com sucesso.");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Erro ao verificar usuário configurado no MongoDB para rclone");
                }
            }

            // 3. O arquivo já foi localizado ou criado acima.
            var pythonExe = FindPythonExe();
            var mountScript = FindMountScript();
            if (string.IsNullOrWhiteSpace(pythonExe) || string.IsNullOrWhiteSpace(mountScript))
            {
                AddServerLog("[NEBULA-MOUNT-ERRO] Python ou mount_drive_n.py não foi encontrado para montar a unidade N:.");
                return false;
            }

            var logFile = Path.Combine(Path.GetTempPath(), "rclone-mount.log");
            AddServerLog($"[NEBULA-MOUNT] Delegando montagem N: ao helper Python (log: {logFile})...");

            var psi = new ProcessStartInfo
            {
                FileName = pythonExe,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add(mountScript);
            psi.ArgumentList.Add("--rclone");
            psi.ArgumentList.Add(rcloneExe);
            psi.ArgumentList.Add("--config");
            psi.ArgumentList.Add(configPath);
            psi.ArgumentList.Add("--port");
            psi.ArgumentList.Add(config.ServerPort.ToString(CultureInfo.InvariantCulture));
            psi.ArgumentList.Add("--drive");
            psi.ArgumentList.Add("N:");
            psi.ArgumentList.Add("--log-file");
            psi.ArgumentList.Add(logFile);

            _rcloneProcess = Process.Start(psi);
            if (_rcloneProcess != null)
            {
                _ = DrainMountOutputAsync(_rcloneProcess.StandardOutput, AddServerLog);
                _ = DrainMountOutputAsync(_rcloneProcess.StandardError, AddServerLog);
            }

            // 5. Polling para verificar se N:\ foi montado e está acessível
            for (var i = 0; i < 20; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (_rcloneProcess != null && _rcloneProcess.HasExited)
                {
                    AddServerLog($"[NEBULA-MOUNT-ERRO] rclone encerrou prematuramente (código: {_rcloneProcess.ExitCode}). Consulte {logFile}");
                    StopAllRcloneProcesses(includeForeignProcesses: true);
                    ScheduleMountRetry();
                    return false;
                }

                if (IsDriveNAccessible())
                {
                    EmitRawLog("Unidade N: montada automaticamente.");
                    return true;
                }

                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
            }

            if (IsDriveNAccessible())
            {
                EmitRawLog("Unidade N: montada automaticamente.");
                return true;
            }

            AddServerLog("[NEBULA-MOUNT-AVISO] Montagem de N: ainda está inicializando em segundo plano.");
            StopAllRcloneProcesses(includeForeignProcesses: true);
            ScheduleMountRetry();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao montar unidade N: via rclone");
            AddServerLog($"[NEBULA-MOUNT-ERRO] Exceção ao montar unidade N: {ex.Message}");
            StopAllRcloneProcesses(includeForeignProcesses: true);
            ScheduleMountRetry();
            return false;
        }
        finally
        {
            _mountLock.Release();
        }
    }

    public async Task<bool> UnmountDriveNAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            StopAllRcloneProcesses();

            for (var i = 0; i < 6; i++)
            {
                if (!Directory.Exists("N:\\"))
                {
                    break;
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            AddServerLog("[NEBULA-MOUNT] Unidade N: desmontada.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desmontar unidade N:");
            return false;
        }
    }

    private string? FindRcloneExe()
    {
        // Prefer the copy shipped with MulletaFlix so the service does not
        // depend on a per-user WinGet installation or a different rclone version.
        var bundledPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "rclone.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rclone.exe")
        };
        foreach (var bundledPath in bundledPaths)
        {
            if (File.Exists(bundledPath))
            {
                return bundledPath;
            }
        }

        // 1. Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathEntries = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in pathEntries)
        {
            try
            {
                var candidate = Path.Combine(entry.Trim('"', ' '), "rclone.exe");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ignorando entrada inválida do PATH ao procurar o rclone");
            }
        }

        // 2. Check WinGet Packages
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wingetDir = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(wingetDir))
        {
            try
            {
                var matches = Directory.GetFiles(wingetDir, "rclone.exe", SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    return matches[0];
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Não foi possível pesquisar os pacotes WinGet por rclone");
            }
        }

        // 3. Check well-known fixed paths
        var wellKnownPaths = new[]
        {
            @"C:\Program Files\rclone\rclone.exe",
            @"C:\rclone\rclone.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "rclone.exe")
        };

        foreach (var p in wellKnownPaths)
        {
            if (File.Exists(p))
            {
                return p;
            }
        }

        return null;
    }

    private string? FindPythonExe()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var candidates = new List<string>();
        foreach (var entry in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            candidates.Add(Path.Combine(entry.Trim('"', ' '), OperatingSystem.IsWindows() ? "python.exe" : "python3"));
        }

        if (OperatingSystem.IsWindows())
        {
            // A Windows service has a different PATH and profile from the
            // interactive user. Prefer machine-wide installations, then the
            // Python launcher available in C:\Windows.
            candidates.Add(@"C:\Python314\python.exe");
            candidates.Add(@"C:\Python313\python.exe");
            candidates.Add(@"C:\Python312\python.exe");
            candidates.Add(@"C:\Program Files\Python314\python.exe");
            candidates.Add(@"C:\Program Files\Python313\python.exe");
            candidates.Add(@"C:\Program Files\Python312\python.exe");
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python314", "python.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python313", "python.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python312", "python.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "py.exe"));
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private string? FindMountScript()
    {
        var candidates = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "mount_drive_n.py"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mount_drive_n.py"),
            Path.Combine(_configManager.CommonApplicationPaths.DataPath, "mount_drive_n.py")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static async Task DrainMountOutputAsync(StreamReader reader, Action<string> logAction)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                logAction(line);
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private string EnsureRcloneConfigFile(int serverPort, string username, string password, string rcloneExe)
    {
        var confPath = Path.Combine(_configManager.CommonApplicationPaths.DataPath, "rclone-nebula.conf");
        var obscuredPassword = ObscureRclonePassword(rcloneExe, password);
        var confContent = $@"[nebula]
type = ftp
host = 127.0.0.1
port = {serverPort}
user = {username}
pass = {obscuredPassword}
explicit_tls = false
no_check_certificate = true
";
        Directory.CreateDirectory(Path.GetDirectoryName(confPath)!);
        File.WriteAllText(confPath, confContent);
        return confPath;
    }

    private (string Username, string Password) EnsureLocalFtpCredentials(NebulaFtpConfiguration config)
    {
        var username = string.IsNullOrWhiteSpace(config.Username) ? "mulleta" : config.Username.Trim();
        var password = config.Password ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(password))
        {
            return (username, password);
        }

        // Cada instalação do MulletaFlix recebe uma credencial própria. Isso
        // evita credenciais fixas no binário ou no instalador.
        password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        config.Username = username;
        config.Password = password;
        _configManager.SaveConfiguration("nebulaftp", config);
        AddServerLog($"[NEBULA-MOUNT] Credencial FTP local provisionada para '{username}'.");
        return (username, password);
    }

    private void StopOwnedRcloneProcess()
    {
        if (_rcloneProcess == null)
        {
            return;
        }

        try
        {
            if (!_rcloneProcess.HasExited)
            {
                _rcloneProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
        finally
        {
            _rcloneProcess.Dispose();
            _rcloneProcess = null;
        }
    }

    private void StopAllRcloneProcesses(bool includeForeignProcesses = false)
    {
        try
        {
            StopOwnedRcloneProcess();

            if (includeForeignProcesses)
            {
                foreach (var process in Process.GetProcessesByName("rclone"))
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Não foi possível encerrar uma instância antiga do rclone");
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }

            var rcloneExe = FindRcloneExe();
            if (!string.IsNullOrWhiteSpace(rcloneExe))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = rcloneExe,
                        Arguments = "unmount N:",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit(2000);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Não foi possível desmontar a unidade N: via rclone");
                }
            }

            // Nunca mate processos rclone globais: eles podem pertencer a outro
            // serviço ou a uma montagem do usuário. O processo iniciado pelo
            // Nebula já foi encerrado acima e o comando unmount é limitado a N:.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Erro ao encerrar processos do rclone");
        }
    }

    private void ScheduleMountRetry()
    {
        if (Interlocked.CompareExchange(ref _mountRetryScheduled, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                    AddServerLog("[NEBULA-MOUNT] Tentando montar novamente a unidade N: após limpar instâncias do rclone...");
                    await MountDriveNAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro na tentativa automática de remontagem da unidade N:");
                }
                finally
                {
                    Interlocked.Exchange(ref _mountRetryScheduled, 0);
                }
            },
            CancellationToken.None);
    }

    private static bool IsDriveNAccessible()
    {
        try
        {
            if (!Directory.Exists("N:\\"))
            {
                return false;
            }

            _ = Directory.GetFileSystemEntries("N:\\");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ObscureRclonePassword(string rcloneExe, string password)
    {
        var psi = new ProcessStartInfo
        {
            FileName = rcloneExe,
            Arguments = $"obscure \"{password.Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar rclone obscure.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException($"rclone obscure falhou: {error}");
        }

        return output;
    }
}
