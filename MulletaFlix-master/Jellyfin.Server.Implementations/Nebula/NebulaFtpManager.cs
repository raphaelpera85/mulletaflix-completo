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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Jellyfin.Server.Implementations.Nebula;
using MulletaFlix.Database.Implementations.Contexts;

namespace MulletaFlix.Server.Implementations.Nebula;

public sealed class NebulaFtpManager : INebulaFtpManager, IDisposable, IAsyncDisposable
{
    private static readonly HttpClient RcloneRemoteControlClient = new() { Timeout = TimeSpan.FromSeconds(5) };
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
    private NebulaPlaybackCache? _playbackCache;
    private readonly NebulaPlaybackCacheAccessor _playbackCacheAccessor = new();
    private NebulaStagingWatcher? _stagingWatcher;
    private NebulaSupabaseSyncService? _supabaseSyncService;
    private NebulaDownloaderEngine? _downloaderEngine;
    private Process? _rcloneProcess;
    private Task? _rcloneStdoutTask;
    private Task? _rcloneStderrTask;
    private CancellationTokenSource? _cleanupCts;
    private Task? _cleanupTask;
    private Task _automaticMountTask = Task.CompletedTask;
    private Task _disposeTask = Task.CompletedTask;

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
    private readonly NebulaDirectoryRefreshQueue _directoryRefreshQueue;
    private readonly NebulaMountRetry _mountRetry;
    private int _disposed;

    // Checkpoint da limpeza incremental: sem ele, um cancelamento descartava todo
    // o avanço e o ciclo seguinte recomeçava da primeira entrada.
    private readonly NebulaCleanupCheckpoint _cleanupCheckpoint = new();
    private int _rcloneRcPort;
    private string? _rcloneRcUsername;
    private string? _rcloneRcPassword;
    private long _nextAutomaticMountAttemptUtcTicks;
    private int _automaticMountAttemptInProgress;
    private int _automaticMountFailures;
    private readonly Dictionary<string, NebulaOperationReplay> _operationReplays = new(StringComparer.Ordinal);
    private readonly object _mediaSuggestionsLock = new();
    private IReadOnlyList<NebulaMediaSuggestionDto> _mediaSuggestions = Array.Empty<NebulaMediaSuggestionDto>();
    private DateTime _mediaSuggestionsRefreshedUtc = DateTime.MinValue;
    private string _mediaSuggestionsRootSignature = string.Empty;
    private static readonly TimeSpan MediaSuggestionsCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EmptyMediaSuggestionsCacheDuration = TimeSpan.FromSeconds(30);
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
        _directoryRefreshQueue = new NebulaDirectoryRefreshQueue(
            RefreshRcloneDirectoryAsync,
            _loggerFactory.CreateLogger<NebulaDirectoryRefreshQueue>());
        _mountRetry = new NebulaMountRetry(async token =>
        {
            if (Volatile.Read(ref _disposed) == 0 && (_isEnvioRunning || _isDownloaderRunning))
            {
                await MountDriveNAsync(token).ConfigureAwait(false);
            }
        }, _loggerFactory.CreateLogger<NebulaMountRetry>());
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

    /// <inheritdoc />
    public IReadOnlyList<NebulaMediaSuggestionDto> SearchMediaSuggestions(string query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return Array.Empty<NebulaMediaSuggestionDto>();
        }

        var catalog = GetMediaSuggestionCatalog();
        var normalizedQuery = NormalizeSuggestionText(query);
        var safeLimit = Math.Clamp(limit, 1, 20);
        return catalog
            .Select(suggestion => (Suggestion: suggestion, Title: NormalizeSuggestionText(suggestion.Title)))
            .Where(match => match.Title.Contains(normalizedQuery, StringComparison.Ordinal))
            .OrderBy(match => match.Title.StartsWith(normalizedQuery, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(match => match.Title.Length)
            .Take(safeLimit)
            .Select(match => match.Suggestion)
            .ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<NebulaMediaSuggestionDto> GetMediaSuggestionCatalog()
    {
        var roots = GetMediaSuggestionRoots();
        var rootSignature = string.Join("\n", roots);
        lock (_mediaSuggestionsLock)
        {
            var cacheDuration = _mediaSuggestions.Count == 0
                ? EmptyMediaSuggestionsCacheDuration
                : MediaSuggestionsCacheDuration;
            if (!string.Equals(_mediaSuggestionsRootSignature, rootSignature, StringComparison.Ordinal)
                || DateTime.UtcNow - _mediaSuggestionsRefreshedUtc >= cacheDuration)
            {
                _mediaSuggestions = BuildMediaSuggestionsCatalog(roots);
                _mediaSuggestionsRootSignature = rootSignature;
                _mediaSuggestionsRefreshedUtc = DateTime.UtcNow;
                _logger.LogInformation(
                    "[NEBULA-REQUESTS] Catálogo STRM atualizado: {TitleCount} títulos encontrados em {RootCount} raízes.",
                    _mediaSuggestions.Count,
                    roots.Length);
            }
        }

        return _mediaSuggestions;
    }

    /// <inheritdoc />
    public bool IsMediaRequestPrioritized(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return (Config.RequestedMediaPriorities ?? Array.Empty<string>())
            .Any(requestedTitle => string.Equals(requestedTitle?.Trim(), title.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private string[] GetMediaSuggestionRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Config.MonitorPaths ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(path)) roots.Add(path.Trim());
        }

        // MonitorPaths can point only at Nebula staging folders. Also include
        // configured Jellyfin libraries, where the user's STRM files may live.
        if (_libraryManager is not null)
        {
            try
            {
                foreach (var location in _libraryManager.GetVirtualFolders().SelectMany(folder => folder.Locations))
                {
                    if (!string.IsNullOrWhiteSpace(location)) roots.Add(location.Trim());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEBULA-REQUESTS] Não foi possível ler as raízes das bibliotecas para o catálogo STRM.");
            }
        }

        return roots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private IReadOnlyList<NebulaMediaSuggestionDto> BuildMediaSuggestionsCatalog(IEnumerable<string> configuredRoots)
    {
        var suggestions = new Dictionary<string, NebulaMediaSuggestionDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var configuredRoot in configuredRoots)
        {
            if (string.IsNullOrWhiteSpace(configuredRoot)
                || NebulaStagingWatcher.IsFileSystemRoot(configuredRoot)
                || !Directory.Exists(configuredRoot))
            {
                continue;
            }

            try
            {
                var countBeforeRoot = suggestions.Count;
                var root = Path.GetFullPath(configuredRoot);
                foreach (var path in Directory.EnumerateFiles(configuredRoot, "*.strm", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    MatchCasing = MatchCasing.CaseInsensitive
                }))
                {
                    var normalizedPath = path.Replace('\\', '/');
                    var folderNames = normalizedPath.Split('/').Select(NormalizeSuggestionText).ToHashSet(StringComparer.Ordinal);
                    var mediaType = folderNames.Contains("doramas") ? "Dorama"
                        : folderNames.Contains("novelas") ? "Novel"
                        : folderNames.Contains("animacoes") || folderNames.Contains("animation") || folderNames.Contains("anime") ? "Animation"
                        : folderNames.Contains("filmes") || folderNames.Contains("movies") ? "Movie"
                        : "Series";

                    var fileTitle = Path.GetFileNameWithoutExtension(path);
                    if (!Regex.IsMatch(fileTitle, @"(?:^|[\s._-])(?:s\d{1,3}e\d{1,3}|\d{1,3}x\d{1,3})(?:$|[\s._-])", RegexOptions.IgnoreCase))
                    {
                        AddMediaSuggestion(fileTitle, mediaType, suggestions);
                    }

                    // Episode STRMs are often named S01E01 or with an episode title.
                    // Also index the nearest non-season folder so users can request a series.
                    DirectoryInfo? directory = new DirectoryInfo(Path.GetDirectoryName(path)!);
                    while (directory is not null && directory.FullName.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!IsGenericMediaFolder(directory.Name))
                        {
                            AddMediaSuggestion(directory.Name, mediaType, suggestions);
                            break;
                        }

                        if (string.Equals(directory.FullName.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        directory = directory.Parent;
                    }
                }
                _logger.LogDebug(
                    "[NEBULA-REQUESTS] Raiz {Root}: {SuggestionCount} sugestões catalogadas.",
                    configuredRoot,
                    suggestions.Count - countBeforeRoot);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NEBULA-REQUESTS] Não foi possível indexar STRM em {Root}.", configuredRoot);
            }
        }

        return suggestions.Values.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddMediaSuggestion(string rawTitle, string mediaType, IDictionary<string, NebulaMediaSuggestionDto> suggestions)
    {
        var title = rawTitle.Trim();
        if (title.Length == 0)
        {
            return;
        }

        // Only an explicit trailing year annotation is release metadata.
        // Numbers in titles (2001, Blade Runner 2049) must not become release years.
        var yearMatch = Regex.Match(title, @"(?:\((?<year>(?:19|20)\d{2})\)|\[(?<year>(?:19|20)\d{2})\])$");
        int? year = yearMatch.Success && int.TryParse(yearMatch.Groups["year"].Value, out var parsedYear) ? parsedYear : null;
        var identity = string.Join('\u001f', mediaType, year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, NormalizeSuggestionText(title));
        suggestions.TryAdd(identity, new NebulaMediaSuggestionDto
        {
            Title = title,
            MediaType = mediaType,
            Year = year
        });
    }

    private static bool IsGenericMediaFolder(string name)
    {
        return Regex.IsMatch(name, @"^(?:s\d{1,3}|season\s*\d{1,3}|temporada\s*\d{1,3}|specials?|extras?|episodes?|epis[oó]dios?)$", RegexOptions.IgnoreCase)
            || Regex.IsMatch(name, @"^(?:series|tvshows|filmes|movies|novelas|doramas|anima[cç][oõ]es)$", RegexOptions.IgnoreCase);
    }

    private static string NormalizeSuggestionText(string value)
    {
        return Regex.Replace(value.Normalize(NormalizationForm.FormD), "\\p{Mn}", string.Empty)
            .ToLowerInvariant()
            .Trim();
    }

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
                        var statusTag = freePct < NebulaDownloaderEngine.MinimumFreeSpacePercentThreshold ? " [CRÍTICO <10%]" : string.Empty;
                        diskList.Add(new NebulaStageDiskDto
                        {
                            Path = path,
                            FreeGb = Math.Round(freeGb, 1),
                            TotalGb = Math.Round(totalGb, 1),
                            FreePercent = Math.Round(freePct, 0),
                            Formatted = $"{path}: {freeGb:F1} GB livres ({freePct:F0}%){statusTag}"
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
                DetailText = _currentDownload.DetailText,
                QueuePosition = _currentDownload.QueuePosition,
                QueueCount = _currentDownload.QueueCount,
                PriorityReason = _currentDownload.PriorityReason,
                NextItemName = _currentDownload.NextItemName
            };
        }

        // Query active uploads (in-memory real-time first, fallback to Mongo)
        if (status.IsEnvioRunning)
        {
            status.UploadQueueSnapshotAvailable = _stagingWatcher != null;
            status.QueuedUploads = _stagingWatcher is not null
                ? _stagingWatcher.GetPendingQueueSnapshot().ToList()
                : await QueryMongoPendingUploadsAsync(config, cancellationToken).ConfigureAwait(false);
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

    public async Task<NebulaUploadQueueSummaryDto> GetUploadQueueSummaryAsync(CancellationToken cancellationToken = default)
    {
        var mongo = _mongoContext;
        if (mongo is null)
        {
            return new NebulaUploadQueueSummaryDto();
        }

        try
        {
            return await mongo.GetUploadQueueSummaryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-HEALTH] Não foi possível consultar o resumo da fila MongoDB.");
            return new NebulaUploadQueueSummaryDto();
        }
    }

    public async Task<List<NebulaFailedUploadDto>> GetTerminalFailedUploadsAsync(CancellationToken cancellationToken = default)
    {
        var mongo = _mongoContext;
        if (mongo is null)
        {
            return [];
        }

        var config = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp") ?? new NebulaFtpConfiguration();
        var stagingRoots = config.StagePaths?.Where(static path => !string.IsNullOrWhiteSpace(path)).ToArray()
            ?? Array.Empty<string>();
        if (stagingRoots.Length == 0)
        {
            stagingRoots = [Path.Combine(AppContext.BaseDirectory, "NebulaStage")];
        }

        try
        {
            return await mongo.GetTerminalFailedUploadsAsync(stagingRoots, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-HEALTH] Não foi possível consultar uploads em falha terminal.");
            return [];
        }
    }

    public async Task<NebulaFailedUploadRetryResultDto> RetryTerminalFailedUploadAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
        {
            return new NebulaFailedUploadRetryResultDto { Message = "Identificador inválido." };
        }

        var mongo = _mongoContext;
        if (mongo is null)
        {
            return new NebulaFailedUploadRetryResultDto { Message = "MongoDB não está conectado." };
        }

        var config = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp") ?? new NebulaFtpConfiguration();
        var stagingRoots = config.StagePaths?.Where(static path => !string.IsNullOrWhiteSpace(path)).ToArray()
            ?? Array.Empty<string>();
        if (stagingRoots.Length == 0)
        {
            stagingRoots = [Path.Combine(AppContext.BaseDirectory, "NebulaStage")];
        }

        try
        {
            var path = await mongo.RetryTerminalUploadAsync(objectId, stagingRoots, cancellationToken).ConfigureAwait(false);
            if (path is null)
            {
                return new NebulaFailedUploadRetryResultDto
                {
                    Message = "Upload não está mais em falha terminal ou o arquivo de origem não existe no staging."
                };
            }

            _stagingWatcher?.EnqueueMediaFromDownloader(path);
            EmitServerLog("INFO", $"[NEBULA] Reprocessamento administrativo enfileirado: {Path.GetFileName(path)}");
            return new NebulaFailedUploadRetryResultDto { Success = true, Message = "Upload recolocado na fila." };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA] Não foi possível reprocessar upload em falha terminal {UploadId}.", id);
            return new NebulaFailedUploadRetryResultDto { Message = "Falha ao recolocar upload na fila; consulte os logs." };
        }
    }

    public async Task<List<NebulaCancellableUploadDto>> GetCancellableUploadsAsync(CancellationToken cancellationToken = default)
    {
        var mongo = _mongoContext;
        if (mongo is null)
        {
            return [];
        }

        try
        {
            return await mongo.GetCancellableUploadsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-HEALTH] Não foi possível consultar uploads canceláveis.");
            return [];
        }
    }

    public async Task<NebulaUploadCancellationResultDto> CancelUploadAsync(
        string id,
        string cancelledBy,
        CancellationToken cancellationToken = default)
    {
        if (!MongoDB.Bson.ObjectId.TryParse(id, out var objectId))
        {
            return new NebulaUploadCancellationResultDto { Message = "Identificador inválido." };
        }

        var mongo = _mongoContext;
        if (mongo is null)
        {
            return new NebulaUploadCancellationResultDto { Message = "MongoDB não está conectado." };
        }

        try
        {
            var result = await mongo.RequestUploadCancellationAsync(objectId, cancelledBy, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                return new NebulaUploadCancellationResultDto { Message = "Upload não encontrado." };
            }

            var status = result.GetValue("status", string.Empty).AsString;
            if (string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return new NebulaUploadCancellationResultDto { Success = true, Message = "Upload cancelado; partes confirmadas foram preservadas." };
            }

            if (string.Equals(status, "uploading", StringComparison.OrdinalIgnoreCase) && result.Contains("cancel_requested_at"))
            {
                return new NebulaUploadCancellationResultDto
                {
                    Success = true,
                    CancellationPending = true,
                    Message = "Cancelamento solicitado. O upload terminará a parte atual e preservará o checkpoint antes de parar."
                };
            }

            return new NebulaUploadCancellationResultDto { Message = "Upload não pode ser cancelado no estado atual." };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA] Não foi possível solicitar cancelamento do upload {UploadId}.", id);
            return new NebulaUploadCancellationResultDto { Message = "Falha ao solicitar cancelamento; consulte os logs." };
        }
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

    private async Task<List<NebulaWorkerItemDto>> QueryMongoPendingUploadsAsync(
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
                var docs = await mongo.GetPendingUploadsAsync(cancellationToken).ConfigureAwait(false);
                foreach (var d in docs)
                {
                    var name = d.Contains("name") ? d["name"].AsString : string.Empty;
                    var state = d.Contains("status") ? d["status"].AsString : "queued";
                    var size = d.Contains("size") && d["size"].IsNumeric ? d["size"].ToInt64() : 0L;
                    results.Add(new NebulaWorkerItemDto
                    {
                        Name = name,
                        DisplayName = name,
                        Status = state,
                        WorkerId = "fila",
                        InfoText = state.Equals("staging", StringComparison.OrdinalIgnoreCase)
                            ? $"Preparando para envio | {name}"
                            : $"Na fila para envio | {name}",
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
        if (Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        if (_isEnvioRunning && _ftpServerHost != null && _ftpServerHost.IsRunning)
        {
            return true;
        }

        if (!await _envioLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            AddServerLog("[NEBULA] Inicialização do Envio já está em andamento. Aguarde a autenticação dos bots...");
            // Never report success while another startup still owns the lock.
            // Mount/watchdog callers must wait for the real FTP startup result.
            await _envioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return _isEnvioRunning && _ftpServerHost?.IsRunning == true;
            }
            finally
            {
                _envioLock.Release();
            }
        }

        var createdMongoContext = false;
        var createdTelegramPool = false;

        try
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

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

            var novelaMigration = await _mongoContext.ScanAndMoveNovelasAsync(cancellationToken).ConfigureAwait(false);
            EmitServerLog(
                novelaMigration.Success ? "INFO" : "WARNING",
                $"[NEBULA-NOVELAS] {novelaMigration.Message}");

            var animacaoMigration = await _mongoContext.NormalizeAnimacoesLibraryAsync(cancellationToken).ConfigureAwait(false);
            EmitServerLog(
                animacaoMigration.Success ? "INFO" : "WARNING",
                $"[NEBULA-ANIMACOES] {animacaoMigration.Message}");

            await _mongoContext.NormalizeDuplicateCategoryRootsAsync(cancellationToken).ConfigureAwait(false);

            // 1.1 Sincronizador com Supabase e verificação de prioridade do banco (restauração se Mongo estiver vazio)
            if (!string.IsNullOrWhiteSpace(config.SupabaseUrl) && !string.IsNullOrWhiteSpace(config.SupabaseKey))
            {
                _supabaseSyncService = new NebulaSupabaseSyncService(
                    _mongoContext,
                    _loggerFactory.CreateLogger<NebulaSupabaseSyncService>(),
                    _usersDbProvider);

                if (!string.IsNullOrWhiteSpace(config.SupabaseProjectRef)
                    && !string.IsNullOrWhiteSpace(config.SupabaseManagementToken))
                {
                    var provisioning = await ProvisionSupabaseSchemaAsync(
                        new NebulaSupabaseProvisionRequest
                        {
                            Url = config.SupabaseUrl,
                            Key = config.SupabaseKey,
                            ProjectRef = config.SupabaseProjectRef,
                            ManagementToken = config.SupabaseManagementToken
                        },
                        cancellationToken).ConfigureAwait(false);
                    AddServerLog(provisioning.Success
                        ? $"[SUPABASE] {provisioning.Message}"
                        : $"[SUPABASE-PROVISIONAMENTO-AVISO] {provisioning.Message}");
                }
            }

            await EnsureDatabaseRestoredIfEmptyAsync(config, AddServerLog, cancellationToken).ConfigureAwait(false);

            // O provedor FTP autentica no MongoDB assim que o socket abre.
            // Portanto, a conta usada pelo rclone precisa existir antes de
            // iniciar o servidor FTP; criá-la durante o mount é tarde demais.
            var (ftpUsername, ftpPassword) = EnsureLocalFtpCredentials();
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
                    logQueueState: logQueueState,
                    getStagingRoots: () => (config.StagePaths ?? Array.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p)),
                    onUploadCompleted: filePath =>
                    {
                        var refreshPath = NebulaUploadEngine.BuildDirectoryRefreshPath(
                            filePath,
                            Path.GetFileName(filePath),
                            config.StagePaths ?? Array.Empty<string>());
                        if (refreshPath != null)
                        {
                            _directoryRefreshQueue.Enqueue(refreshPath);
                        }

                        return Task.CompletedTask;
                    });
            }
            else
            {
                // Modo streamOnly: uploadEngine nulo, bots só servem streaming via FTP/HTTP
                _uploadEngine = null;
                EmitServerLog("INFO", "[NEBULA] Modo Somente Streaming: Upload Engine desativado (bots não enviam arquivos).");
            }

            // 4. Servidor FTP C# nativo
            var effectiveCachePath = !string.IsNullOrWhiteSpace(config.PlaybackCachePath)
                ? config.PlaybackCachePath
                : _configManager.CommonApplicationPaths.CachePath;

            _playbackCache = new NebulaPlaybackCache(
                effectiveCachePath,
                _loggerFactory.CreateLogger<NebulaPlaybackCache>(),
                maxCacheBytes: (long)config.PlaybackCacheMaxSizeGb * 1024 * 1024 * 1024,
                minimumFreeSpaceBytes: (long)config.PlaybackCacheMinimumFreeSpaceGb * 1024 * 1024 * 1024);
            _playbackCacheAccessor.Set(_playbackCache);

            _ftpServerHost = new NebulaFtpServerHost(
                _mongoContext,
                _telegramPool,
                _uploadEngine, // pode ser null em streamOnly
                _playbackCacheAccessor,
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
                config.MaxActiveConnections,
                _playbackCacheAccessor);
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
                foreach (var title in config.RequestedMediaPriorities ?? Array.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        _stagingWatcher.PrioritizeUpload(string.Empty, title);
                    }
                }

                // Seed persisted requests before Start launches the restoration
                // loop and workers; otherwise the first restored item could be
                // dequeued before its priority is registered.
                _stagingWatcher.Start(allWatchDirs, config.MaxWorkers);
            }

            // 6. Inicia sincronização contínua de background para manter o Supabase sempre atualizado
            if (_supabaseSyncService != null && !string.IsNullOrWhiteSpace(config.SupabaseUrl) && !string.IsNullOrWhiteSpace(config.SupabaseKey))
            {
                if (config.SupabaseAutoBackup)
                {
                    var intervalMinutes = NebulaFtpConfiguration.DefaultSupabaseAutoBackupIntervalHours * 60;
                    _supabaseSyncService.StartContinuousSync(
                        config.SupabaseUrl,
                        config.SupabaseKey,
                        intervalMinutes: intervalMinutes,
                        progressAction: AddServerLog,
                        usersBackupCompleted: RecordUsersBackupResult);
                    config.SupabaseAutoBackupIntervalHours = NebulaFtpConfiguration.DefaultSupabaseAutoBackupIntervalHours;
                    AddServerLog("[SUPABASE] Serviço de sincronização contínua e backup automático ativado (Intervalo: 1 hora).");
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
        _mountRetry.Cancel();
        await _envioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var stopped = false;

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

            _playbackCache?.Dispose();
            _playbackCache = null;
            _playbackCacheAccessor.Set(null);

            if (_uploadEngine != null)
            {
                await _uploadEngine.DisposeAsync().ConfigureAwait(false);
                _uploadEngine = null;
            }

            stopped = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop NebulaFTP server");
            AddServerLog($"[ERRO] Falha ao parar serviços: {ex.Message}");
        }
        finally
        {
            try
            {
                await DisposeSharedRuntimeResourcesAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                stopped = false;
                _logger.LogError(cleanupException, "Falha ao liberar recursos compartilhados após parar o Envio.");
            }

            _envioLock.Release();
        }

        if (stopped)
        {
            AddServerLog("NebulaFTP Server nativo em C# encerrado com sucesso.");
        }

        return stopped;
    }

    public async Task<bool> StartPlaybackPrefetchAsync(string mediaPath, CancellationToken cancellationToken = default)
    {
        var streamServer = _httpStreamServer;
        if (streamServer is null)
        {
            return false;
        }

        try
        {
            return await streamServer.StartPlaybackPrefetchAsync(mediaPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao iniciar o pré-cache da mídia; a reprodução continuará sem pré-cache.");
            return false;
        }
    }

    public async Task<bool> CancelPlaybackPrefetchAsync(string mediaPath, CancellationToken cancellationToken = default)
    {
        var streamServer = _httpStreamServer;
        if (streamServer is null)
        {
            return false;
        }

        try
        {
            return await streamServer.CancelPlaybackPrefetchAsync(mediaPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao cancelar o pré-cache da mídia {MediaPath}; o cache expirará pela política normal.", mediaPath);
            return false;
        }
    }

    /// <inheritdoc />
    public void PrioritizeMedia(string mediaPath, string? seriesPath = null, string? seriesName = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(mediaPath)
                && string.IsNullOrWhiteSpace(seriesPath)
                && !string.IsNullOrWhiteSpace(seriesName))
            {
                var requestedTitle = seriesName.Trim();
                _configManager.UpdateConfiguration("nebulaftp", current =>
                {
                    var config = ((NebulaFtpConfiguration)current).CreateSnapshot();
                    var priorities = (config.RequestedMediaPriorities ?? Array.Empty<string>()).ToList();
                    if (priorities.Contains(requestedTitle, StringComparer.OrdinalIgnoreCase))
                    {
                        return current;
                    }

                    priorities.Add(requestedTitle);
                    config.RequestedMediaPriorities = priorities.ToArray();
                    return config;
                });

                _downloaderEngine?.PrioritizeTarget(string.Empty, seriesName);
                _stagingWatcher?.PrioritizeUpload(string.Empty, seriesName);
                _logger.LogInformation(
                    "[NEBULA-PRIORITY] Solicitação registrada para prioridade de download e upload: {Title}",
                    seriesName.Trim());
                AddDownloaderLog($"[PRIORIDADE] Solicitação registrada para download e envio: '{seriesName.Trim()}'.");
            }

            if (!string.IsNullOrWhiteSpace(mediaPath))
            {
                _downloaderEngine?.PrioritizeTarget(mediaPath, seriesName);
                _stagingWatcher?.PrioritizeUpload(mediaPath, seriesName);
            }

            if (!string.IsNullOrWhiteSpace(seriesPath))
            {
                _downloaderEngine?.PrioritizeTarget(seriesPath, seriesName);
                _stagingWatcher?.PrioritizeUpload(seriesPath, seriesName);

                PrioritizeAllSeriesFiles(seriesPath, seriesName);
            }

            _downloaderEngine?.WakeUp();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-PRIORITY] Erro ao priorizar mídia: {Path}", mediaPath);
        }
    }

    /// <inheritdoc />
    public void PrioritizeItem(BaseItem item)
    {
        if (item == null || !Config.Enabled)
        {
            return;
        }

        try
        {
            bool isSeriesLike = item is Series or Season or Episode;
            string? seriesPath = null;
            string? seriesName = null;
            string? mediaPath = item.Path;

            if (item is Episode episode)
            {
                isSeriesLike = true;
                var series = episode.Series ?? (_libraryManager != null && episode.SeriesId != Guid.Empty ? _libraryManager.GetItemById(episode.SeriesId) as Series : null);
                if (series != null)
                {
                    seriesPath = series.Path;
                    seriesName = series.Name;
                }
                else if (!string.IsNullOrWhiteSpace(episode.Path))
                {
                    var parent = Directory.GetParent(episode.Path);
                    if (parent != null && parent.Name.StartsWith("Season", StringComparison.OrdinalIgnoreCase))
                    {
                        seriesPath = parent.Parent?.FullName;
                        seriesName = parent.Parent?.Name;
                    }
                    else
                    {
                        seriesPath = parent?.FullName;
                        seriesName = parent?.Name;
                    }
                }
            }
            else if (item is Season season)
            {
                isSeriesLike = true;
                var series = season.Series;
                seriesPath = series?.Path ?? Directory.GetParent(season.Path)?.FullName;
                seriesName = series?.Name ?? Directory.GetParent(season.Path)?.Name;
            }
            else if (item is Series series)
            {
                isSeriesLike = true;
                seriesPath = series.Path;
                seriesName = series.Name;
            }
            else if (!string.IsNullOrWhiteSpace(item.Path))
            {
                var dir = Path.GetDirectoryName(item.Path);
                var file = Path.GetFileName(item.Path);
                var classified = NebulaUploadEngine.ClassifyMediaType(dir, file);
                if (classified is "SERIE" or "NOVELA" or "ANIMACAO" or "DORAMA")
                {
                    isSeriesLike = true;
                    var parent = Directory.GetParent(item.Path);
                    if (parent != null && parent.Name.StartsWith("Season", StringComparison.OrdinalIgnoreCase))
                    {
                        seriesPath = parent.Parent?.FullName;
                        seriesName = parent.Parent?.Name;
                    }
                    else
                    {
                        seriesPath = parent?.FullName;
                        seriesName = parent?.Name;
                    }
                }
            }

            // Checa se a mídia ou a série existe nas pastas com strm
            bool hasStrm = (!string.IsNullOrWhiteSpace(mediaPath) && (mediaPath.EndsWith(".strm", StringComparison.OrdinalIgnoreCase) || File.Exists(Path.ChangeExtension(mediaPath, ".strm"))))
                || (!string.IsNullOrWhiteSpace(seriesPath) && Directory.Exists(seriesPath) && Directory.EnumerateFiles(seriesPath, "*.strm", SearchOption.AllDirectories).Any())
                || HasStrmInMonitorSources(item.Name, seriesName);

            if (!hasStrm)
            {
                return;
            }

            if (isSeriesLike)
            {
                var name = seriesName ?? item.Name;
                _logger.LogInformation("[NEBULA-PRIORITY] Solicitação de obra seriada ({Type}): '{Series}'. Priorizando todas as temporadas e episódios (.strm) no download e upload!", item.GetType().Name, name);
                AddDownloaderLog($"[PRIORIDADE] Solicitação de obra: '{name}'. Priorizando todas as temporadas e episódios (.strm)!");
                PrioritizeMedia(mediaPath ?? string.Empty, seriesPath, name);
            }
            else
            {
                _logger.LogInformation("[NEBULA-PRIORITY] Solicitação de mídia (.strm): '{Name}' ({Path}). Priorizando download e upload!", item.Name, mediaPath);
                AddDownloaderLog($"[PRIORIDADE] Solicitação de mídia: '{item.Name}'. Priorizando download e upload!");
                PrioritizeMedia(mediaPath ?? string.Empty, null, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-PRIORITY] Falha ao avaliar prioridade para o item: {Name}", item.Name);
        }
    }

    private void PrioritizeAllSeriesFiles(string seriesPath, string? seriesName)
    {
        try
        {
            if (Directory.Exists(seriesPath))
            {
                foreach (var strm in Directory.EnumerateFiles(seriesPath, "*.strm", SearchOption.AllDirectories))
                {
                    _downloaderEngine?.PrioritizeTarget(strm, seriesName);
                    _stagingWatcher?.PrioritizeUpload(strm, seriesName);
                }
            }

            if (!string.IsNullOrWhiteSpace(seriesName))
            {
                var monitorSources = Config.MonitorPaths ?? Array.Empty<string>();
                foreach (var src in monitorSources)
                {
                    if (!Directory.Exists(src))
                    {
                        continue;
                    }

                    try
                    {
                        foreach (var catDir in Directory.EnumerateDirectories(src))
                        {
                            var targetSeriesDir = Path.Combine(catDir, seriesName);
                            if (Directory.Exists(targetSeriesDir))
                            {
                                _downloaderEngine?.PrioritizeTarget(targetSeriesDir, seriesName);
                                _stagingWatcher?.PrioritizeUpload(targetSeriesDir, seriesName);
                                foreach (var strm in Directory.EnumerateFiles(targetSeriesDir, "*.strm", SearchOption.AllDirectories))
                                {
                                    _downloaderEngine?.PrioritizeTarget(strm, seriesName);
                                    _stagingWatcher?.PrioritizeUpload(strm, seriesName);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[NEBULA-PRIORITY] Erro ao varrer monitor source {Src} para a série {Name}", src, seriesName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-PRIORITY] Erro ao varrer arquivos da série {SeriesPath}", seriesPath);
        }
    }

    private bool HasStrmInMonitorSources(string itemName, string? seriesName)
    {
        try
        {
            var monitorSources = Config.MonitorPaths ?? Array.Empty<string>();
            foreach (var src in monitorSources)
            {
                if (!Directory.Exists(src))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(seriesName))
                {
                    foreach (var catDir in Directory.EnumerateDirectories(src))
                    {
                        var targetSeriesDir = Path.Combine(catDir, seriesName);
                        if (Directory.Exists(targetSeriesDir))
                        {
                            return true;
                        }
                    }
                }

                foreach (var file in Directory.EnumerateFiles(src, "*.strm", SearchOption.AllDirectories))
                {
                    var fName = Path.GetFileName(file);
                    if ((!string.IsNullOrWhiteSpace(seriesName) && fName.Contains(seriesName, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(itemName) && fName.Contains(itemName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // Ignora erro de acesso a disco
        }

        return false;
    }

    public NebulaPlaybackCacheStatusDto GetPlaybackCacheStatus()
    {
        var configuredPath = Config.PlaybackCachePath ?? string.Empty;
        var effectivePath = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : _configManager.CommonApplicationPaths.CachePath;

        var fullCachePath = Path.Combine(effectivePath, "nebula-playback");
        long totalBytes = 0;
        int fileCount = 0;
        int activeLeases = 0;

        if (_playbackCache != null)
        {
            totalBytes = _playbackCache.GetCacheSizeBytes();
            fileCount = _playbackCache.GetCachedFilesCount();
            activeLeases = _playbackCache.ActiveLeasesCount;
            fullCachePath = _playbackCache.CachePath;
        }

        double freeGb = 0;
        double totalGb = 0;
        try
        {
            var driveRoot = Path.GetPathRoot(Path.GetFullPath(fullCachePath));
            if (!string.IsNullOrEmpty(driveRoot))
            {
                var drive = new DriveInfo(driveRoot);
                if (drive.IsReady)
                {
                    freeGb = Math.Round(drive.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0, 2);
                    totalGb = Math.Round(drive.TotalSize / 1024.0 / 1024.0 / 1024.0, 2);
                }
            }
        }
        catch
        {
        }

        return new NebulaPlaybackCacheStatusDto
        {
            IsAvailable = _playbackCache != null,
            ConfiguredPath = configuredPath,
            EffectivePath = fullCachePath,
            TotalSizeBytes = totalBytes,
            FormattedSize = FormatBytes(totalBytes),
            CachedFilesCount = fileCount,
            ActiveLeasesCount = activeLeases,
            CacheHits = _playbackCache?.CacheHits ?? 0,
            CacheMisses = _playbackCache?.CacheMisses ?? 0,
            TelegramFetchCount = _playbackCache?.TelegramFetchCount ?? 0,
            TelegramFetchFailures = _playbackCache?.TelegramFetchFailures ?? 0,
            TelegramFetchCancellations = _playbackCache?.TelegramFetchCancellations ?? 0,
            AverageTelegramFetchLatencyMs = _playbackCache?.AverageTelegramFetchLatencyMs ?? 0,
            ActivePrefetchCount = _playbackCache?.ActivePrefetchCount ?? 0,
            QueuedPrefetchCount = _playbackCache?.QueuedPrefetchCount ?? 0,
            CacheErrors = _playbackCache?.CacheErrors ?? 0,
            CacheCleanupRuns = _playbackCache?.CacheCleanupRuns ?? 0,
            CacheCleanupFailures = _playbackCache?.CacheCleanupFailures ?? 0,
            CacheCleanupSkipped = _playbackCache?.CacheCleanupSkipped ?? 0,
            LastCacheCleanupDurationMs = _playbackCache?.LastCacheCleanupDurationMs ?? 0,
            LastCacheCleanupUtc = _playbackCache?.LastCacheCleanupUtc,
            FreeSpaceGb = freeGb,
            TotalSpaceGb = totalGb,
            MaxCacheSizeBytes = _playbackCache?.MaxCacheBytes ?? (long)Config.PlaybackCacheMaxSizeGb * 1024 * 1024 * 1024,
            MinimumFreeSpaceBytes = _playbackCache?.MinimumFreeSpaceBytes ?? (long)Config.PlaybackCacheMinimumFreeSpaceGb * 1024 * 1024 * 1024
        };
    }

    public Task<bool> ClearPlaybackCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cache = _playbackCache;
            if (cache is null)
            {
                var configuredPath = Config.PlaybackCachePath;
                var effectivePath = !string.IsNullOrWhiteSpace(configuredPath)
                    ? configuredPath
                    : _configManager.CommonApplicationPaths.CachePath;
                cache = new NebulaPlaybackCache(
                    effectivePath,
                    _loggerFactory.CreateLogger<NebulaPlaybackCache>(),
                    maxCacheBytes: (long)Config.PlaybackCacheMaxSizeGb * 1024 * 1024 * 1024,
                    minimumFreeSpaceBytes: (long)Config.PlaybackCacheMinimumFreeSpaceGb * 1024 * 1024 * 1024);
                try
                {
                    var cleared = cache.ClearCache();
                    return Task.FromResult(cleared);
                }
                finally
                {
                    cache.Dispose();
                }
            }
            else
            {
                return Task.FromResult(cache.ClearCache());
            }
        }
        catch (OperationCanceledException)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA] Falha ao limpar cache de reprodução.");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> UpdatePlaybackCachePathAsync(
        string? newPath,
        int? maxCacheSizeGb = null,
        int? minimumFreeSpaceGb = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (maxCacheSizeGb is < 1 or > 4096 || minimumFreeSpaceGb is < 0 or > 1024)
            {
                return false;
            }

            await _envioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _sharedRuntimeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (Volatile.Read(ref _disposed) != 0)
                    {
                        return false;
                    }

                    var trimmed = (newPath ?? Config.PlaybackCachePath)?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(trimmed))
                    {
                        Directory.CreateDirectory(trimmed);
                    }

                    var config = Config;

                    var effective = !string.IsNullOrWhiteSpace(trimmed)
                        ? trimmed
                        : _configManager.CommonApplicationPaths.CachePath;

                    var maxCacheBytes = (long)(maxCacheSizeGb ?? config.PlaybackCacheMaxSizeGb) * 1024 * 1024 * 1024;
                    var minimumFreeSpaceBytes = (long)(minimumFreeSpaceGb ?? config.PlaybackCacheMinimumFreeSpaceGb) * 1024 * 1024 * 1024;
                    var requestedCacheRoot = Path.GetFullPath(Path.Combine(effective, "nebula-playback"));
                    if (_playbackCache != null
                        && string.Equals(
                            Path.TrimEndingDirectorySeparator(Path.GetFullPath(_playbackCache.CachePath)),
                            Path.TrimEndingDirectorySeparator(requestedCacheRoot),
                            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    {
                        var saved = SavePlaybackCacheSettings(trimmed, maxCacheSizeGb, minimumFreeSpaceGb);
                        _playbackCache.UpdateLimits((long)saved.PlaybackCacheMaxSizeGb * 1024 * 1024 * 1024, (long)saved.PlaybackCacheMinimumFreeSpaceGb * 1024 * 1024 * 1024);
                        _logger.LogInformation("[NEBULA] Limites do cache de reprodução atualizados sem interromper a mídia ativa.");
                        return true;
                    }

                    var oldCache = _playbackCache;
                    var newCache = new NebulaPlaybackCache(
                        effective,
                        _loggerFactory.CreateLogger<NebulaPlaybackCache>(),
                        maxCacheBytes: maxCacheBytes,
                        minimumFreeSpaceBytes: minimumFreeSpaceBytes);
                    try
                    {
                        var saved = SavePlaybackCacheSettings(trimmed, maxCacheSizeGb, minimumFreeSpaceGb);
                        newCache.UpdateLimits((long)saved.PlaybackCacheMaxSizeGb * 1024 * 1024 * 1024, (long)saved.PlaybackCacheMinimumFreeSpaceGb * 1024 * 1024 * 1024);
                    }
                    catch
                    {
                        newCache.Dispose();
                        throw;
                    }

                    _playbackCache = newCache;
                    _playbackCacheAccessor.Set(newCache);

                    if (oldCache != null)
                    {
                        _ = oldCache.DisposeWhenIdleAsync();
                    }

                    _httpStreamServer?.SetPlaybackCache(newCache);

                    _logger.LogInformation("[NEBULA] Caminho do cache de reprodução atualizado para: {Path}", effective);
                    return true;
                }
                finally
                {
                    _sharedRuntimeLock.Release();
                }
            }
            finally
            {
                _envioLock.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA] Falha ao atualizar caminho do cache de reprodução para {Path}", newPath);
            return false;
        }
    }

    private NebulaFtpConfiguration SavePlaybackCacheSettings(string path, int? maxCacheSizeGb, int? minimumFreeSpaceGb)
    {
        return (NebulaFtpConfiguration)_configManager.UpdateConfiguration("nebulaftp", current =>
        {
            var config = ((NebulaFtpConfiguration)current).CreateSnapshot();
            config.PlaybackCachePath = path;
            config.PlaybackCacheMaxSizeGb = maxCacheSizeGb ?? config.PlaybackCacheMaxSizeGb;
            config.PlaybackCacheMinimumFreeSpaceGb = minimumFreeSpaceGb ?? config.PlaybackCacheMinimumFreeSpaceGb;
            if (config.PlaybackCacheMaxSizeGb is < 1 or > 4096 || config.PlaybackCacheMinimumFreeSpaceGb is < 0 or > 1024)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCacheSizeGb), "Os limites vigentes do cache são inválidos.");
            }

            return config;
        });
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024m) >= 1 && counter < suffixes.Length - 1)
        {
            number /= 1024m;
            counter++;
        }

        return $"{number:n1} {suffixes[counter]}";
    }

    public async Task<bool> StartDownloaderAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        if (_isDownloaderRunning && _downloaderEngine != null && _downloaderEngine.IsRunning)
        {
            return true;
        }

        if (!await _downloaderLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            AddDownloaderLog("[NEBULA] Inicialização do Downloader já está em andamento.");
            // Do not claim success before the existing startup has finished.
            await _downloaderLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return _isDownloaderRunning && _downloaderEngine?.IsRunning == true;
            }
            finally
            {
                _downloaderLock.Release();
            }
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
        try
        {
            await _envioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _downloaderLock.Release();
            throw;
        }

        try
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

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
            foreach (var title in config.RequestedMediaPriorities ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    _downloaderEngine.PrioritizeTarget(string.Empty, title);
                }
            }
            _downloaderEngine.OnUploadReady += filePath => _stagingWatcher?.EnqueueMediaFromDownloader(filePath);
            _downloaderEngine.OnLog += msg => AddDownloaderLog(msg);
            _downloaderEngine.OnProgressChanged += st =>
            {
                var queueProgress = _downloaderEngine.GetQueueProgress();
                _currentDownload.Name = st.Name;
                _currentDownload.TotalMb = st.TotalMb;
                _currentDownload.DoneMb = st.DoneMb;
                _currentDownload.Percentage = st.Percentage;
                _currentDownload.StageStep = st.StageStep;
                _currentDownload.DetailText = st.DetailText;
                _currentDownload.QueuePosition = queueProgress.Position;
                _currentDownload.QueueCount = queueProgress.Count;
                _currentDownload.PriorityReason = queueProgress.Reason;
                _currentDownload.NextItemName = queueProgress.NextItemName;
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
                _mountRetry.Schedule(TimeSpan.Zero);
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
        _mountRetry.Cancel();
        await _downloaderLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        var stopped = false;

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

            stopped = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop STRM Downloader");
            AddDownloaderLog($"[ERRO] Falha ao parar Downloader: {ex.Message}");
        }
        finally
        {
            try
            {
                await DisposeSharedRuntimeResourcesAsync().ConfigureAwait(false);
            }
            catch (Exception cleanupException)
            {
                stopped = false;
                _logger.LogError(cleanupException, "Falha ao liberar recursos compartilhados após parar o Downloader.");
            }

            _downloaderLock.Release();
        }

        if (stopped)
        {
            AddDownloaderLog("STRM Downloader encerrado.");
        }

        return stopped;
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

            if (_uploadEngine != null || _downloaderEngine != null || _stagingWatcher != null
                || _ftpServerHost != null || _httpStreamServer != null || _supabaseSyncService != null)
            {
                throw new InvalidOperationException("Nebula components still own shared runtime resources after incomplete shutdown.");
            }

            var cleanupTask = _cleanupTask;
            _cleanupCts?.Cancel();

            if (cleanupTask != null)
            {
                try
                {
                    await cleanupTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning("[NEBULA-SHUTDOWN] Limpeza ainda em execução. Recursos compartilhados preservados para nova tentativa de encerramento.");
                    throw;
                }
                catch (OperationCanceledException) when (_cleanupCts?.IsCancellationRequested == true)
                {
                    // Cooperative cleanup cancellation has completed; disposal is safe.
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

    public async Task<NebulaNovelaMigrationResult> ScanAndMoveNovelasAsync(CancellationToken cancellationToken = default)
    {
        var mongo = _mongoContext;
        if (mongo == null)
        {
            return new NebulaNovelaMigrationResult
            {
                Success = false,
                Message = "O MongoDB do Nebula ainda não está inicializado. Inicie o envio e tente novamente."
            };
        }

        return await mongo.ScanAndMoveNovelasAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<NebulaAnimacaoMigrationResult> NormalizeAnimacoesLibraryAsync(CancellationToken cancellationToken = default)
    {
        var mongo = _mongoContext;
        if (mongo == null)
        {
            return new NebulaAnimacaoMigrationResult
            {
                Success = false,
                Message = "O MongoDB do Nebula ainda não está inicializado. Inicie o envio e tente novamente."
            };
        }

        return await mongo.NormalizeAnimacoesLibraryAsync(cancellationToken).ConfigureAwait(false);
    }


    private void StartContinuousCleanup(NebulaFtpConfiguration config)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            StartContinuousCleanupCore(config);
        }
    }

    private void StartContinuousCleanupCore(NebulaFtpConfiguration config)
    {
        if (_cleanupTask is { IsCompleted: false })
        {
            if (_cleanupCts?.IsCancellationRequested == true)
            {
                throw new InvalidOperationException("Previous Nebula cleanup is still stopping. Retry startup after it completes.");
            }

            // Envio and Downloader share one cleanup loop.
            return;
        }

        _cleanupCts?.Cancel();
        _cleanupCts?.Dispose();
        _cleanupCts = new CancellationTokenSource();
        var cleanupToken = _cleanupCts.Token;

        var sources = (config.MonitorPaths ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p)
                && !p.StartsWith("N:", StringComparison.OrdinalIgnoreCase)
                && !NebulaStagingWatcher.IsFileSystemRoot(p))
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
        EmitCleanupLog("Modo: Contínuo (a cada 15 min)");
        EmitCleanupLog("Dry-run: False");
        EmitCleanupLog("===========================================");
        EmitCleanupLog(string.Empty);

        _cleanupTask = Task.Run(() => RunContinuousCleanupLoopAsync(sources, cleanupToken), cleanupToken);
    }

    private async Task RunContinuousCleanupLoopAsync(List<string> sources, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // A cleanup cycle walks every monitored source recursively AND materialises the full
                // set of completed documents from Mongo just to build a path HashSet. At the previous
                // 30 second interval that was 2,880 full library walks plus 2,880 full BSON result
                // sets per day, which was the largest single driver of the host's page-file pressure
                // on a 15.7 GB machine. Leftovers only need to be reclaimed eventually, so a much
                // longer interval costs nothing real.
                await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken).ConfigureAwait(false);
                if (_mongoContext != null && !_streamOnly)
                {
                    await RunCleanupCycleAsync(sources, cancellationToken).ConfigureAwait(false);
                }

                // Watchdog: Auto-monta N: apenas se UseMappedDrive=true (compatibilidade legada)
                var cfg = NormalizeRuntimeConfiguration(Config);
                // Telegram pool is created early during startup. It is not proof
                // that FTP/upload/download services are ready. Waiting for the
                // explicit running flags prevents the watchdog from mounting N:
                // while StartEnvioAsync still authenticates bots.
                if (cfg.UseMappedDrive && (_isEnvioRunning || _isDownloaderRunning) && !IsDriveNAccessible())
                {
                    var nowTicks = DateTime.UtcNow.Ticks;
                    var nextAttemptTicks = Interlocked.Read(ref _nextAutomaticMountAttemptUtcTicks);
                    if (nowTicks >= nextAttemptTicks
                        && Interlocked.CompareExchange(ref _automaticMountAttemptInProgress, 1, 0) == 0)
                    {
                        _logger.LogWarning("[NEBULA-WATCHDOG] Unidade N: inacessível (UseMappedDrive=true). Iniciando auto-recuperação...");
                        AddServerLog("[NEBULA-WATCHDOG] Unidade N: inacessível. Remontando automaticamente...");
                        _automaticMountTask = Task.Run(
                            async () =>
                            {
                                try
                                {
                                    var mounted = await MountDriveNAsync(cancellationToken).ConfigureAwait(false);
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
                // Travessia incremental: `GetFiles(..., AllDirectories)` materializava
                // a árvore inteira antes de tratar o primeiro arquivo, o que numa raiz
                // grande é um pico de alocação e um atraso longo sem progresso visível.
                // `EnumerateFiles` entrega sob demanda e o checkpoint permite retomar
                // de onde o ciclo anterior parou, em vez de recomeçar do início.
                var resumeMarker = _cleanupCheckpoint.GetResumeMarker(source);
                var entries = NebulaCleanupCheckpoint.EnumerateFrom(
                    Directory.EnumerateFiles(source, "*.*", SearchOption.AllDirectories),
                    resumeMarker);

                if (!string.IsNullOrWhiteSpace(resumeMarker))
                {
                    EmitCleanupLog($"Limpeza retomada em {source} após {_cleanupCheckpoint.GetProcessedCount(source)} arquivo(s) já verificados.");
                }

                var completedTraversal = true;
                foreach (var file in entries)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // O progresso permanece registrado: o próximo ciclo retoma
                        // a partir da última entrada tratada.
                        completedTraversal = false;
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
                        _cleanupCheckpoint.Record(source, file);
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

                    _cleanupCheckpoint.Record(source, file);
                }

                if (completedTraversal)
                {
                    // Raiz percorrida por inteiro: zera o marcador para que o ciclo
                    // seguinte também considere arquivos criados antes dele.
                    var processed = _cleanupCheckpoint.GetProcessedCount(source);
                    _cleanupCheckpoint.Complete(source);
                    if (processed > 0)
                    {
                        _logger.LogDebug(
                            "[NEBULA-CLEANUP] Raiz {Source} percorrida por completo: {Processed} arquivo(s) verificados.",
                            source,
                            processed);
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

        var tokenToSave = request.Token.Trim();
        _configManager.UpdateConfiguration("nebulaftp", current =>
        {
            var config = ((NebulaFtpConfiguration)current).CreateSnapshot();
            var tokens = (config.BotTokens ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (request.Index.HasValue && request.Index.Value >= 1 && request.Index.Value <= tokens.Count)
            {
                tokens[request.Index.Value - 1] = tokenToSave;
            }
            else if (!tokens.Contains(tokenToSave, StringComparer.OrdinalIgnoreCase))
            {
                tokens.Add(tokenToSave);
            }

            config.BotTokens = string.Join(",", tokens);
            return config;
        });

        return GetBots();
    }

    public List<NebulaBotDto> DeleteBot(int index)
    {
        _configManager.UpdateConfiguration("nebulaftp", current =>
        {
            var config = ((NebulaFtpConfiguration)current).CreateSnapshot();
            var tokens = (config.BotTokens ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
            if (index < 1 || index > tokens.Count)
            {
                return current;
            }

            tokens.RemoveAt(index - 1);
            config.BotTokens = string.Join(",", tokens);
            return config;
        });

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

        if (!NebulaSupabaseSyncService.IsServiceRoleKey(key))
        {
            return new NebulaSupabaseTestResponseDto
            {
                Success = false,
                Message = NebulaSupabaseSyncService.ServiceRoleKeyRequiredMessage,
                StatusCode = 403
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

    public async Task<NebulaSupabaseProvisionResultDto> ProvisionSupabaseSchemaAsync(NebulaSupabaseProvisionRequest? request, CancellationToken cancellationToken = default)
    {
        var config = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp") ?? new NebulaFtpConfiguration();
        var url = string.IsNullOrWhiteSpace(request?.Url) ? config.SupabaseUrl.Trim() : request.Url.Trim();
        var key = string.IsNullOrWhiteSpace(request?.Key) ? config.SupabaseKey.Trim() : request.Key.Trim();
        var projectRef = string.IsNullOrWhiteSpace(request?.ProjectRef) ? config.SupabaseProjectRef.Trim() : request.ProjectRef.Trim();
        var managementToken = string.IsNullOrWhiteSpace(request?.ManagementToken) ? config.SupabaseManagementToken.Trim() : request.ManagementToken.Trim();

        if (!IsSafeSupabaseUrl(url, out var safeUrl) || string.IsNullOrWhiteSpace(key))
        {
            return new NebulaSupabaseProvisionResultDto { Success = false, Message = "Informe uma URL HTTPS válida e a Secret key do Supabase." };
        }

        if (!Regex.IsMatch(projectRef, "^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$", RegexOptions.CultureInvariant))
        {
            return new NebulaSupabaseProvisionResultDto { Success = false, Message = "Informe um Project ID válido do Supabase." };
        }

        if (string.IsNullOrWhiteSpace(managementToken))
        {
            return new NebulaSupabaseProvisionResultDto { Success = false, Message = "Informe o token da Management API para criar a estrutura automaticamente." };
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"https://api.supabase.com/v1/projects/{Uri.EscapeDataString(projectRef)}/database/query")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { query = GetSupabaseSqlScript(), read_only = false }), Encoding.UTF8, "application/json")
            };
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", managementToken);
            requestMessage.Headers.Add("User-Agent", "MulletaFlix-Server/12.0");

            using var response = await client.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (body.Length > 512) body = body[..512] + "…";
                return new NebulaSupabaseProvisionResultDto { Success = false, Message = $"A Management API recusou o provisionamento (HTTP {(int)response.StatusCode}): {body}" };
            }

            config.SupabaseUrl = safeUrl;
            config.SupabaseKey = key;
            config.SupabaseProjectRef = projectRef;
            config.SupabaseManagementToken = string.Empty;
            _configManager.SaveConfiguration("nebulaftp", config);
            AddServerLog("[SUPABASE] Estrutura inicial criada/validada pela Management API. Token administrativo removido da configuração.");
            return new NebulaSupabaseProvisionResultDto { Success = true, Message = "Estrutura do Supabase criada/validada com sucesso. O token administrativo foi removido da configuração." };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Falha ao provisionar schema do Supabase pela Management API.");
            return new NebulaSupabaseProvisionResultDto { Success = false, Message = $"Não foi possível provisionar o Supabase: {ex.Message}" };
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
            LastBackupAttemptTime = config.SupabaseLastBackupAttemptTime,
            LastBackupFailed = config.SupabaseLastBackupFailed,
            LastBackupProcessedFilesCount = config.SupabaseLastBackupProcessedFilesCount,
            LastBackupProcessedUsersCount = config.SupabaseLastBackupProcessedUsersCount,
            LastUsersBackupTime = config.SupabaseLastUsersBackupTime,
            LastUsersBackupStatus = config.SupabaseLastUsersBackupStatus,
            LastUsersBackupCount = config.SupabaseLastUsersBackupCount,
            LastUsersBackupFailed = config.SupabaseLastUsersBackupFailed,
            LastRestoreTime = config.SupabaseLastRestoreTime,
            LastRestoreStatus = config.SupabaseLastRestoreStatus,
            LastRestoreFailed = config.SupabaseLastRestoreFailed,
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

    public async Task<NebulaSupabaseBackupResultDto> BackupMongoToSupabaseAsync(string? idempotencyKey = null, bool forceFull = false, CancellationToken cancellationToken = default)
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

            var result = await syncService.PerformBackupAsync(config.SupabaseUrl, config.SupabaseKey, ReportBackupProgress, forceFull, cancellationToken).ConfigureAwait(false);
            if (result.Success)
            {
                AddServerLog($"[SUPABASE] {result.Message}");
                CompleteMaintenanceOperation("succeeded");
            }
            else
            {
                AddServerLog($"[SUPABASE-ERRO] Falha na sincronização: {result.Message}");
                CompleteMaintenanceOperation("failed", result.Message);
            }

            RecordMongoBackupResult(result);
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
            RecordMongoBackupResult(failedResult);
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

    public async Task<NebulaSupabaseBackupResultDto> BackupUsersToSupabaseAsync(CancellationToken cancellationToken = default)
    {
        var config = NormalizeRuntimeConfiguration(Config);
        if (_supabaseSyncService == null)
        {
            var unavailableResult = new NebulaSupabaseBackupResultDto { Success = false, Message = "Serviço de sincronização do Supabase indisponível." };
            RecordUsersBackupResult(unavailableResult);
            return unavailableResult;
        }

        AddServerLog("[SUPABASE-USERS] Iniciando backup exclusivo dos usuários do MulletaFlix...");
        var result = await _supabaseSyncService.PerformUsersBackupAsync(
            config.SupabaseUrl,
            config.SupabaseKey,
            AddServerLog,
            cancellationToken).ConfigureAwait(false);
        RecordUsersBackupResult(result);
        AddServerLog(result.Success ? $"[SUPABASE-USERS] {result.Message}" : $"[SUPABASE-USERS-ERRO] {result.Message}");
        return result;
    }

    private void RecordUsersBackupResult(NebulaSupabaseBackupResultDto result)
    {
        _configManager.UpdateConfiguration("nebulaftp", current =>
        {
            var config = ((NebulaFtpConfiguration)current).CreateSnapshot();
            config.SupabaseLastUsersBackupFailed = !result.Success;
            config.SupabaseLastUsersBackupTime = DateTime.UtcNow;
            config.SupabaseLastUsersBackupStatus = result.Success
                ? result.Message
                : $"Falha no backup de usuários: {result.Message}";
            if (result.Success)
            {
                config.SupabaseLastUsersBackupCount = result.UsersBackedUp;
            }

            return config;
        });
    }

    internal void RecordMongoBackupResult(NebulaSupabaseBackupResultDto result)
    {
        _configManager.UpdateConfiguration("nebulaftp", current =>
        {
            var config = ((NebulaFtpConfiguration)current).CreateSnapshot();
            ApplyMongoBackupResult(config, result);
            if (result.Success)
            {
                config.SupabaseLastBackupTime = DateTime.UtcNow;
            }

            config.SupabaseLastBackupStatus = result.Success
                ? $"Backup do MongoDB realizado com sucesso ({result.FilesBackedUp} arquivos, {result.UsersBackedUp} usuários FTP) em {DateTime.Now:dd/MM/yyyy HH:mm:ss}"
                : $"Falha no backup às {DateTime.Now:dd/MM/yyyy HH:mm:ss}: {result.Message}";
            return config;
        });
    }

    internal static void ApplyMongoBackupResult(NebulaFtpConfiguration config, NebulaSupabaseBackupResultDto result)
    {
        config.SupabaseLastBackupAttemptTime = result.Timestamp;
        config.SupabaseLastBackupFailed = !result.Success;
        config.SupabaseLastBackupProcessedFilesCount = result.Success ? result.FilesBackedUp : null;
        config.SupabaseLastBackupProcessedUsersCount = result.Success ? result.UsersBackedUp : null;
    }

    public async Task<NebulaSupabaseRestoreResultDto> RestoreUsersFromSupabaseAsync(CancellationToken cancellationToken = default)
    {
        var config = NormalizeRuntimeConfiguration(Config);
        if (_supabaseSyncService == null)
        {
            return new NebulaSupabaseRestoreResultDto { Success = false, Message = "Serviço de sincronização do Supabase indisponível." };
        }

        AddServerLog("[SUPABASE-USERS] Iniciando restauração exclusiva dos usuários do MulletaFlix...");
        var result = await _supabaseSyncService.PerformUsersRestoreAsync(
            config.SupabaseUrl,
            config.SupabaseKey,
            AddServerLog,
            cancellationToken).ConfigureAwait(false);
        AddServerLog(result.Success ? $"[SUPABASE-USERS] {result.Message}" : $"[SUPABASE-USERS-ERRO] {result.Message}");
        return result;
    }

    public async Task<NebulaSupabaseRestoreResultDto> RestoreSupabaseToMongoAsync(string? idempotencyKey = null, bool forceFull = false, CancellationToken cancellationToken = default)
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

        AddServerLog("[SUPABASE-RESTORE] Iniciando restauração do MongoDB a partir do Supabase (Nativo C#)...");
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

            var result = await syncService.PerformRestoreAsync(config.SupabaseUrl, config.SupabaseKey, ReportRestoreProgress, forceFull, cancellationToken).ConfigureAwait(false);
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

            RecordMongoRestoreResult(result.Success, result.Message, result.FilesRestored, result.UsersRestored);
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
            RecordMongoRestoreResult(false, ex.Message, 0, 0);
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

    internal void RecordMongoRestoreResult(bool success, string message, int filesRestored, int usersRestored)
    {
        _configManager.UpdateConfiguration("nebulaftp", current =>
        {
            var config = ((NebulaFtpConfiguration)current).CreateSnapshot();
            config.SupabaseLastRestoreTime = DateTime.UtcNow;
            config.SupabaseLastRestoreFailed = !success;
            config.SupabaseLastRestoreStatus = success
                ? $"Restauração do MongoDB realizada com sucesso ({filesRestored} arquivos, {usersRestored} usuários) em {DateTime.Now:dd/MM/yyyy HH:mm:ss}"
                : $"Falha na restauração às {DateTime.Now:dd/MM/yyyy HH:mm:ss}: {message}";
            return config;
        });
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

    public async Task<bool> SendNotificationAsync(string messageHtml, string? imagePath = null, string? targetChannelId = null, CancellationToken cancellationToken = default)
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

            _ = int.TryParse(config.ApiId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var apiId);
            _ = long.TryParse(config.ChatId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatId);
            await using var tempPool = new NebulaTelegramPool(apiId, config.ApiHash, botTokens, chatId, GetSessionsDirectory(), _loggerFactory.CreateLogger<NebulaTelegramPool>());
            if (!string.IsNullOrWhiteSpace(imagePath) && await tempPool.SendPhotoAsync(imagePath, messageHtml, targetChannelId, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            return await tempPool.SendMessageAsync(messageHtml, targetChannelId, cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(imagePath) && await pool.SendPhotoAsync(imagePath, messageHtml, targetChannelId, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        return await pool.SendMessageAsync(messageHtml, targetChannelId, cancellationToken).ConfigureAwait(false);
    }

    public NebulaTelegramNotificationSettingsDto GetTelegramNotificationSettings()
    {
        var config = Config;
        var rawChatIds = string.IsNullOrWhiteSpace(config.TelegramNotificationChatIds)
            ? config.ChatId
            : config.TelegramNotificationChatIds;

        return new NebulaTelegramNotificationSettingsDto
        {
            Enabled = config.TelegramNotificationsEnabled,
            IntervalSeconds = Math.Clamp(config.TelegramNotificationIntervalSeconds, 1, 60),
            ChatIds = ParseTelegramChatIds(rawChatIds)
        };
    }

    public bool SaveTelegramNotificationSettings(NebulaTelegramNotificationSettingsRequest request)
    {
        var chatIds = ParseTelegramChatIds(request.ChatIds);
        if (chatIds.Count == 0)
        {
            return false;
        }

        _configManager.UpdateConfiguration("nebulaftp", current =>
        {
            var config = ((NebulaFtpConfiguration)current).CreateSnapshot();
            config.TelegramNotificationsEnabled = request.Enabled;
            config.TelegramNotificationIntervalSeconds = Math.Clamp(request.IntervalSeconds, 1, 60);
            config.TelegramNotificationChatIds = string.Join(",", chatIds);
            config.ChatId = chatIds[0];
            return config;
        });
        return true;
    }

    public NebulaNotificationsSettingsDto GetNotificationsSettings()
    {
        var config = Config;
        var rawChannelIds = string.IsNullOrWhiteSpace(config.NotificationsChannelIds)
            ? (string.IsNullOrWhiteSpace(config.TelegramNotificationChatIds) ? config.ChatId : config.TelegramNotificationChatIds)
            : config.NotificationsChannelIds;

        return new NebulaNotificationsSettingsDto
        {
            Enabled = config.NotificationsEnabled,
            IntervalSeconds = Math.Clamp(config.NotificationsIntervalSeconds > 0 ? config.NotificationsIntervalSeconds : config.TelegramNotificationIntervalSeconds, 1, 60),
            ChannelIds = ParseTelegramChatIds(rawChannelIds),
            PublicServerUrl = NormalizePublicServerUrl(config.PublicServerUrl)
        };
    }

    public bool SaveNotificationsSettings(NebulaNotificationsSettingsRequest request)
    {
        var channelIds = ParseTelegramChatIds(request.ChannelIds);
        if (channelIds.Count == 0)
        {
            return false;
        }

        var interval = Math.Clamp(request.IntervalSeconds, 1, 60);
        _configManager.UpdateConfiguration("nebulaftp", current =>
        {
            var config = ((NebulaFtpConfiguration)current).CreateSnapshot();
            config.NotificationsEnabled = request.Enabled;
            config.NotificationsIntervalSeconds = interval;
            config.NotificationsChannelIds = string.Join(",", channelIds);
            config.PublicServerUrl = NormalizePublicServerUrl(request.PublicServerUrl ?? config.PublicServerUrl);
            return config;
        });
        return true;
    }

    private static string NormalizePublicServerUrl(string? value)
    {
        var url = string.IsNullOrWhiteSpace(value) ? "http://mulletaflix.duckdns.org:8096" : value.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || parsed.Scheme is not ("http" or "https")
            || parsed.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || parsed.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || parsed.Host.Equals("::1", StringComparison.OrdinalIgnoreCase))
        {
            return "http://mulletaflix.duckdns.org:8096";
        }

        return parsed.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    internal static List<string> ParseTelegramChatIds(string? rawChatIds)
    {
        return (rawChatIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                // A failed stop keeps owned resources alive. Allow DisposeAsync
                // to retry after the process becomes stoppable.
                if (_disposeTask.IsFaulted)
                {
                    _disposeTask = Task.Run(() => DisposeAfterWorkersAsync(Task.CompletedTask));
                }

                return;
            }

            _mountRetry.Dispose();
            var callbacks = _cleanupCts?.CancelAsync() ?? Task.CompletedTask;
            _disposeTask = Task.Run(() => DisposeAfterWorkersAsync(callbacks));
        }
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        Task pending;
        lock (_lock)
        {
            pending = _disposeTask;
        }

        await pending.ConfigureAwait(false);
    }

    private async Task DisposeAfterWorkersAsync(Task callbacks)
    {
        var acquired = new List<SemaphoreSlim>();
        try
        {
            var cleanup = _cleanupTask;
            if (cleanup != null)
            {
                try
                {
                    await cleanup.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_cleanupCts?.IsCancellationRequested == true)
                {
                }
            }

            await callbacks.ConfigureAwait(false);
            await _mountRetry.WaitForIdleAsync().ConfigureAwait(false);
            // Cleanup is drained before capturing its last watchdog attempt.
            await _automaticMountTask.ConfigureAwait(false);
            // Downloader startup already owns its gate before acquiring Envio.
            // Follow that order or disposal can deadlock against startup.
            // Maintenance jobs acquire maintenance then shared-runtime locks.
            // Drain them in that order before releasing Mongo/Supabase resources.
            foreach (var gate in new[] { _downloaderLock, _envioLock, _mountLock, _maintenanceLock, _sharedRuntimeLock })
            {
                await gate.WaitAsync().ConfigureAwait(false);
                acquired.Add(gate);
            }

            // Normal disposal owns only this manager's process. Other server
            // instances and independent rclone mounts must remain untouched.
            if (!StopOwnedRcloneProcess())
            {
                throw new InvalidOperationException("Não foi possível parar o processo rclone pertencente ao Nebula; recursos preservados para nova tentativa.");
            }

            _directoryRefreshQueue.Dispose();

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
                await _telegramPool.DisposeAsync().ConfigureAwait(false);
                _telegramPool = null;
            }

            if (_mongoContext != null)
            {
                _mongoContext.Dispose();
                _mongoContext = null;
            }

            _cleanupCts?.Dispose();
            _cleanupCts = null;
            _cleanupTask = null;
            _playbackCache?.Dispose();
            _playbackCache = null;
            _playbackCacheAccessor.Set(null);
            _isEnvioRunning = false;
            _isDownloaderRunning = false;
            // Keep managed transition gates valid for callers already waiting;
            // they observe _disposed after acquisition and can release safely.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao concluir descarte assíncrono do NebulaFtpManager.");
            throw;
        }
        finally
        {
            for (var index = acquired.Count - 1; index >= 0; index--)
            {
                acquired[index].Release();
            }
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
        // Earlier versions forced this value to 24 on every startup. Normalize old
        // configurations to the new hourly schedule instead of retaining that legacy value.
        config.SupabaseAutoBackupIntervalHours = NebulaFtpConfiguration.DefaultSupabaseAutoBackupIntervalHours;
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
            var userCount = await _mongoContext.CountUsersAsync(cancellationToken).ConfigureAwait(false);
            var appUserCount = _supabaseSyncService == null
                ? 0
                : await _supabaseSyncService.GetMulletaFlixUserCountAsync(cancellationToken).ConfigureAwait(false);
            var appUserBackupCount = _supabaseSyncService == null
                ? 0
                : await _supabaseSyncService.GetMulletaFlixUserBackupCountAsync(config.SupabaseUrl, config.SupabaseKey, cancellationToken).ConfigureAwait(false);
            if (userCount == 0 || appUserCount == 0 || appUserBackupCount > appUserCount)
            {
                logAction?.Invoke($"[DATABASE-INIT] Usuários locais incompletos detectados (usuários FTP: {userCount}, usuários MulletaFlix: {appUserCount}/{appUserBackupCount} no backup). Verificando Supabase...");
                _logger.LogInformation("[DATABASE-INIT] Usuários locais incompletos detectados (FTP: {FtpUsers}, MulletaFlix: {AppUsers}/{BackupUsers}). Iniciando auto-restauração de usuários a partir do Supabase...", userCount, appUserCount, appUserBackupCount);

                _supabaseSyncService ??= new NebulaSupabaseSyncService(_mongoContext, _loggerFactory.CreateLogger<NebulaSupabaseSyncService>(), _usersDbProvider);
                var restoreResult = await _supabaseSyncService.PerformRestoreAsync(config.SupabaseUrl, config.SupabaseKey, cancellationToken).ConfigureAwait(false);

                if (restoreResult.Success && (restoreResult.FilesRestored > 0 || restoreResult.UsersRestored > 0))
                {
                    logAction?.Invoke($"[DATABASE-INIT] Auto-restauração de usuários concluída! {restoreResult.UsersRestored} usuários recuperados do Supabase.");
                    _logger.LogInformation("[DATABASE-INIT] Auto-restauração de usuários concluída: {Users} usuários restaurados.", restoreResult.UsersRestored);
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
        cancellationToken.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

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
            if (Volatile.Read(ref _disposed) != 0)
            {
                return false;
            }

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
                ScheduleMountRetry();
                return false;
            }

            var rcloneExe = FindRcloneExe();
            if (string.IsNullOrWhiteSpace(rcloneExe))
            {
                AddServerLog("[NEBULA-MOUNT-ERRO] rclone.exe não foi encontrado no sistema. Verifique a instalação do rclone.");
                return false;
            }

            if (!StopOwnedRcloneProcess())
            {
                AddServerLog("[NEBULA-MOUNT-ERRO] Processo próprio anterior não encerrou. Montagem adiada.");
                return false;
            }

            for (var i = 0; i < 6; i++)
            {
                if (!Directory.Exists("N:\\"))
                {
                    break;
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            if (Directory.Exists("N:\\"))
            {
                AddServerLog("[NEBULA-MOUNT-ERRO] Unidade N: ocupada sem montagem pertencente a esta instância. Nenhum processo externo será encerrado.");
                return false;
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

            var (ftpUsername, ftpPassword) = EnsureLocalFtpCredentials();
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

            var rcPort = FindAvailableLoopbackPort();
            var rcUsername = "mulletaflix-" + Guid.NewGuid().ToString("N")[..12];
            var rcPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            Volatile.Write(ref _rcloneRcPort, rcPort);
            _rcloneRcUsername = rcUsername;
            _rcloneRcPassword = rcPassword;

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
            psi.ArgumentList.Add("--rc-port");
            psi.ArgumentList.Add(rcPort.ToString(CultureInfo.InvariantCulture));
            psi.Environment["RCLONE_RC_USER"] = rcUsername;
            psi.Environment["RCLONE_RC_PASS"] = rcPassword;

            _rcloneProcess = Process.Start(psi);
            if (_rcloneProcess != null)
            {
                _rcloneStdoutTask = DrainMountOutputAsync(_rcloneProcess.StandardOutput, AddServerLog);
                _rcloneStderrTask = DrainMountOutputAsync(_rcloneProcess.StandardError, AddServerLog);
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
                    StopOwnedRcloneProcess();
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
            StopOwnedRcloneProcess();
            ScheduleMountRetry();
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StopOwnedRcloneProcess();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao montar unidade N: via rclone");
            AddServerLog($"[NEBULA-MOUNT-ERRO] Exceção ao montar unidade N: {ex.Message}");
            StopOwnedRcloneProcess();
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
        _mountRetry.Cancel();
        await _mountLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_rcloneProcess == null)
            {
                AddServerLog("[NEBULA-MOUNT] Nenhuma montagem própria para encerrar. Montagens externas preservadas.");
                return true;
            }

            if (!StopOwnedRcloneProcess())
            {
                return false;
            }

            for (var i = 0; i < 6; i++)
            {
                if (!Directory.Exists("N:\\"))
                {
                    break;
                }

                await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            if (Directory.Exists("N:\\"))
            {
                AddServerLog("[NEBULA-MOUNT-AVISO] Processo próprio encerrado, mas N: continua ocupada. Nenhum processo externo foi encerrado.");
                return false;
            }

            AddServerLog("[NEBULA-MOUNT] Unidade N: desmontada.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao desmontar unidade N:");
            return false;
        }
        finally
        {
            _mountLock.Release();
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
        catch (IOException)
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
idle_timeout = 15s
";
        Directory.CreateDirectory(Path.GetDirectoryName(confPath)!);
        File.WriteAllText(confPath, confContent);
        return confPath;
    }

    internal (string Username, string Password) EnsureLocalFtpCredentials()
    {
        var provisioned = false;
        var saved = (NebulaFtpConfiguration)_configManager.UpdateConfiguration("nebulaftp", current =>
        {
            var existing = (NebulaFtpConfiguration)current;
            if (!string.IsNullOrWhiteSpace(existing.Password))
            {
                return current;
            }

            var config = existing.CreateSnapshot();
            config.Username = string.IsNullOrWhiteSpace(existing.Username) ? "mulleta" : existing.Username.Trim();
            // Each installation gets its own credential; never overwrite an existing password.
            config.Password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            provisioned = true;
            return config;
        });
        var username = string.IsNullOrWhiteSpace(saved.Username) ? "mulleta" : saved.Username.Trim();
        if (provisioned)
        {
            AddServerLog($"[NEBULA-MOUNT] Credencial FTP local provisionada para '{username}'.");
        }

        return (username, saved.Password ?? string.Empty);
    }

    private bool StopOwnedRcloneProcess()
    {
        if (_rcloneProcess == null)
        {
            return true;
        }

        try
        {
            if (!_rcloneProcess.HasExited)
            {
                _rcloneProcess.Kill(entireProcessTree: true);
                if (!_rcloneProcess.WaitForExit(5000))
                {
                    _logger.LogWarning("[NEBULA-MOUNT] Processo próprio não encerrou dentro do prazo. Referência preservada para nova tentativa.");
                    return false;
                }
            }

            var outputTasks = new[] { _rcloneStdoutTask, _rcloneStderrTask }
                .Where(task => task is not null)
                .Cast<Task>()
                .ToArray();
            if (outputTasks.Length > 0)
            {
                try
                {
                    if (!Task.WaitAll(outputTasks, 5000))
                    {
                        _logger.LogWarning("[NEBULA-MOUNT] Leitores de saída não encerraram dentro do prazo. Referência do processo preservada para nova tentativa.");
                        return false;
                    }
                }
                catch (AggregateException ex) when (outputTasks.All(task => task.IsCompleted))
                {
                    _logger.LogWarning(ex, "[NEBULA-MOUNT] Um leitor de saída terminou com erro; todos encerraram e a liberação do processo continuará.");
                }
            }

            _rcloneProcess.Dispose();
            _rcloneProcess = null;
            _rcloneStdoutTask = null;
            _rcloneStderrTask = null;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-MOUNT] Falha ao encerrar processo próprio. Referência preservada para nova tentativa.");
            return false;
        }
    }

    private void ScheduleMountRetry()
    {
        _mountRetry.Schedule(TimeSpan.FromSeconds(3));
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

    private async Task RefreshRcloneDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        var port = Volatile.Read(ref _rcloneRcPort);
        var username = _rcloneRcUsername;
        var password = _rcloneRcPassword;
        if (port <= 0 || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)
            || _rcloneProcess == null || _rcloneProcess.HasExited)
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}/vfs/refresh");
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new StringContent(
                JsonSerializer.Serialize(new { dir = directory }),
                Encoding.UTF8,
                "application/json");

            using var response = await RcloneRemoteControlClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (json.RootElement.TryGetProperty("error", out var error) && !string.IsNullOrWhiteSpace(error.GetString()))
            {
                throw new InvalidOperationException($"rclone recusou o refresh da pasta virtual: {error.GetString()}");
            }

            _logger.LogDebug("[NEBULA-MOUNT] Cache da pasta virtual atualizado: {Directory}", directory);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // MongoDB já confirmou o upload. Uma falha de cache nunca reverte nem falha a mídia.
            _logger.LogWarning(ex, "[NEBULA-MOUNT] Refresh da pasta virtual falhou após upload concluído: {Directory}", directory);
        }
    }

    private static int FindAvailableLoopbackPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
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
