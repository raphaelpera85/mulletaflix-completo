using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Cache compartilhado, temporário e em disco para blocos de mídias reproduzidas pelo Nebula.
/// O cache é separado por mídia e expira 1 hora depois da última atividade não utilizada.
/// </summary>
public sealed class NebulaPlaybackCache : IDisposable
{
    /// <summary>
    /// Tempo de expiração padrão de entradas de reprodução não utilizadas (1 hora).
    /// </summary>
    public static readonly TimeSpan DefaultEntryLifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Intervalo padrão do timer de limpeza periódica do cache (5 minutos).
    /// </summary>
    public static readonly TimeSpan DefaultCleanupInterval = TimeSpan.FromMinutes(5);

    public const long DefaultMaxCacheBytes = 50L * 1024 * 1024 * 1024;

    public const long DefaultMinimumFreeSpaceBytes = 2L * 1024 * 1024 * 1024;

    private const int MaxConcurrentMediaPrefetches = 2;
    private const int MaxQueuedMediaPrefetches = 4;
    private const int MaxConcurrentAheadChunkPrefetches = 2;

    private readonly string _rootPath;
    private readonly ILogger<NebulaPlaybackCache> _logger;
    private readonly TimeSpan _entryLifetime;
    private long _maxCacheBytes;
    private long _minimumFreeSpaceBytes;
    private readonly SemaphoreSlim _storageGate = new(1, 1);
    private readonly ConcurrentDictionary<string, int> _activeMedia = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _cancelWhenIdle = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _lastActivity = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SharedFetch> _inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PrefetchState> _prefetches = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<PrefetchState, byte> _allPrefetches = new();
    private readonly SemaphoreSlim _prefetchConcurrency = new(MaxConcurrentMediaPrefetches, MaxConcurrentMediaPrefetches);
    private readonly SemaphoreSlim _aheadPrefetchConcurrency = new(MaxConcurrentAheadChunkPrefetches, MaxConcurrentAheadChunkPrefetches);
    private readonly Timer _cleanupTimer;
    private int _disposed;
    private long _cacheHits;
    private long _cacheMisses;
    private long _telegramFetchCount;
    private long _telegramFetchFailures;
    private long _telegramFetchDurationTicks;
    private long _cacheErrors;

    public NebulaPlaybackCache(
        string cachePath,
        ILogger<NebulaPlaybackCache> logger,
        TimeSpan? entryLifetime = null,
        long maxCacheBytes = DefaultMaxCacheBytes,
        long minimumFreeSpaceBytes = DefaultMinimumFreeSpaceBytes)
    {
        if (string.IsNullOrWhiteSpace(cachePath))
        {
            throw new ArgumentException("O caminho do cache de reprodução é obrigatório.", nameof(cachePath));
        }

        _rootPath = Path.Combine(cachePath, "nebula-playback");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _entryLifetime = entryLifetime ?? DefaultEntryLifetime;
        _maxCacheBytes = maxCacheBytes > 0 ? maxCacheBytes : long.MaxValue;
        _minimumFreeSpaceBytes = Math.Max(0, minimumFreeSpaceBytes);
        Directory.CreateDirectory(_rootPath);
        _cleanupTimer = new Timer(static state => ((NebulaPlaybackCache)state!).CleanupExpiredEntries(), this, DefaultCleanupInterval, DefaultCleanupInterval);
    }

    /// <summary>
    /// Caminho raiz do diretório de cache em disco.
    /// </summary>
    public string CachePath => _rootPath;

    /// <summary>
    /// Quantidade de mídias ativas atualmente em reprodução (com lease ativo).
    /// </summary>
    public int ActiveLeasesCount => _activeMedia.Count;

    internal int PendingPrefetchCount => _prefetches.Count;

    public long CacheHits => Interlocked.Read(ref _cacheHits);

    public long CacheMisses => Interlocked.Read(ref _cacheMisses);

    public long TelegramFetchCount => Interlocked.Read(ref _telegramFetchCount);

    public long TelegramFetchFailures => Interlocked.Read(ref _telegramFetchFailures);

    public double AverageTelegramFetchLatencyMs => TelegramFetchCount == 0
        ? 0
        : TimeSpan.FromTicks(Interlocked.Read(ref _telegramFetchDurationTicks)).TotalMilliseconds / TelegramFetchCount;

    /// <summary>
    /// Número de tarefas de pré-cache que já adquiriram um slot. Callbacks de descarte que continuam
    /// após o término da tarefa não contam como reprodução/pré-cache ativo.
    /// </summary>
    public int ActivePrefetchCount => _allPrefetches.Keys.Count(static state => state.HasConcurrencySlot);

    /// <summary>
    /// Número de tarefas admitidas que ainda aguardam um slot de pré-cache.
    /// </summary>
    public int QueuedPrefetchCount => Math.Max(0, _allPrefetches.Count - ActivePrefetchCount);

    public long CacheErrors => Interlocked.Read(ref _cacheErrors);

    public long MaxCacheBytes => Volatile.Read(ref _maxCacheBytes);

    public long MinimumFreeSpaceBytes => Volatile.Read(ref _minimumFreeSpaceBytes);

    public void UpdateLimits(long maxCacheBytes, long minimumFreeSpaceBytes)
    {
        if (maxCacheBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCacheBytes), "A cota deve ser positiva.");
        }

        if (minimumFreeSpaceBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumFreeSpaceBytes), "A reserva não pode ser negativa.");
        }

        _storageGate.Wait();
        try
        {
            Interlocked.Exchange(ref _maxCacheBytes, maxCacheBytes);
            Interlocked.Exchange(ref _minimumFreeSpaceBytes, minimumFreeSpaceBytes);
            try
            {
                TrimConfiguredLimits();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "[NEBULA-CACHE] Novos limites foram aplicados, mas não foi possível concluir a evicção imediata.");
            }
        }
        finally
        {
            _storageGate.Release();
        }
    }

    public async Task DisposeWhenIdleAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            (SharedFetch[] Fetches, PrefetchState[] Prefetches)? resources;
            await _storageGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Volatile.Read(ref _disposed) != 0)
                {
                    return;
                }

                resources = BeginDisposeUnderGate(onlyWhenIdle: true);
            }
            finally
            {
                _storageGate.Release();
            }

            if (resources.HasValue)
            {
                FinishDispose(resources.Value);
                return;
            }
        }
    }

    /// <summary>
    /// Retorna o tamanho total ocupado em bytes pelo cache no disco.
    /// </summary>
    public long GetCacheSizeBytes()
    {
        return TryGetCacheSizeBytes(out var size) ? size : 0;
    }

    private bool TryGetCacheSizeBytes(out long size)
    {
        size = 0;
        try
        {
            if (!Directory.Exists(_rootPath))
            {
                return true;
            }

            size = new DirectoryInfo(_rootPath).EnumerateFiles("*", SearchOption.AllDirectories).Sum(static file => file.Length);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "[NEBULA-CACHE] Falha ao calcular tamanho do cache.");
            return false;
        }
    }

    /// <summary>
    /// Retorna a quantidade total de arquivos em cache no disco.
    /// </summary>
    public int GetCachedFilesCount()
    {
        try
        {
            if (!Directory.Exists(_rootPath))
            {
                return 0;
            }

            var dirInfo = new DirectoryInfo(_rootPath);
            return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Count();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-CACHE] Falha ao contar arquivos do cache.");
            return 0;
        }
    }

    /// <summary>
    /// Limpa todos os dados em cache no disco que não estejam sob lease ativo ou em prefetch.
    /// </summary>
    public bool ClearCache()
    {
        var success = true;
        _storageGate.Wait();
        try
        {
            if (!Directory.Exists(_rootPath))
            {
                return true;
            }

            foreach (var directory in Directory.GetDirectories(_rootPath))
            {
                var mediaKey = Path.GetFileName(directory);
                if (_activeMedia.ContainsKey(mediaKey)
                    || _prefetches.ContainsKey(mediaKey)
                    || _inflight.Keys.Any(key => key.StartsWith(mediaKey + ":", StringComparison.Ordinal)))
                {
                    continue;
                }

                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        try
                        {
                            File.SetAttributes(file, FileAttributes.Normal);
                        }
                        catch
                        {
                        }
                    }

                    Directory.Delete(directory, recursive: true);
                    _lastActivity.TryRemove(mediaKey, out _);
                }
                catch (Exception ex)
                {
                    success = false;
                    _logger.LogWarning(ex, "[NEBULA-CACHE] Falha ao limpar diretório de mídia {Directory}.", directory);
                }
            }
        }
        catch (Exception ex)
        {
            success = false;
            _logger.LogWarning(ex, "[NEBULA-CACHE] Falha ao limpar cache geral.");
        }
        finally
        {
            _storageGate.Release();
        }

        return success;
    }

    /// <summary>Marca uma mídia como em reprodução. Enquanto houver uma sessão, seus blocos não são removidos.</summary>
    public IDisposable Acquire(string mediaKey)
    {
        _storageGate.Wait();
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var normalized = NormalizeMediaKey(mediaKey);
            _cancelWhenIdle.TryRemove(normalized, out _);
            if (_prefetches.TryGetValue(normalized, out var prefetch))
            {
                prefetch.TryRevokeCancellationRequest();
            }

            _activeMedia.AddOrUpdate(normalized, 1, static (_, count) => count + 1);
            Touch(normalized);
            return new PlaybackLease(this, normalized);
        }
        finally
        {
            _storageGate.Release();
        }
    }

    public async Task<byte[]> GetOrFetchChunkAsync(
        string mediaKey,
        int partIndex,
        int chunkIndex,
        int expectedLength,
        Func<CancellationToken, Task<byte[]>> fetch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fetch);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (expectedLength <= 0)
        {
            return Array.Empty<byte>();
        }

        var normalized = NormalizeMediaKey(mediaKey);
        Touch(normalized);
        var directory = GetMediaDirectory(normalized);
        var path = Path.Combine(directory, $"{partIndex:D6}-{chunkIndex:D8}.bin");
        if (TryReadComplete(path, expectedLength, out var cached))
        {
            Interlocked.Increment(ref _cacheHits);
            return cached;
        }

        Interlocked.Increment(ref _cacheMisses);

        var inflightKey = $"{normalized}:{partIndex}:{chunkIndex}";
        while (true)
        {
            SharedFetch shared;
            Task<byte[]> sharedTask;
            await _storageGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                var candidate = new SharedFetch(token => DownloadAndPersistAsync(path, expectedLength, fetch, token), _logger);
                shared = _inflight.GetOrAdd(inflightKey, candidate);
                if (!shared.TryJoin(out sharedTask))
                {
                    ((ICollection<KeyValuePair<string, SharedFetch>>)_inflight)
                        .Remove(new KeyValuePair<string, SharedFetch>(inflightKey, shared));
                    if (TryReadComplete(path, expectedLength, out cached))
                    {
                        Interlocked.Increment(ref _cacheHits);
                        return cached;
                    }

                    continue;
                }

                if (ReferenceEquals(shared, candidate))
                {
                    _ = RemoveCompletedFetchAsync(inflightKey, shared, sharedTask);
                }
            }
            finally
            {
                _storageGate.Release();
            }

            try
            {
                return await sharedTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                shared.Leave();
            }
        }
    }

    internal async Task<byte[]> GetOrFetchAheadChunkAsync(
        string mediaKey,
        int partIndex,
        int chunkIndex,
        int expectedLength,
        Func<CancellationToken, Task<byte[]>> fetch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_aheadPrefetchConcurrency.Wait(0, cancellationToken))
        {
            // Ahead é especulativo: não crie uma fila sem limite; a leitura demandada baixa o chunk se necessário.
            return Array.Empty<byte>();
        }

        try
        {
            return await GetOrFetchChunkAsync(
                mediaKey,
                partIndex,
                chunkIndex,
                expectedLength,
                fetch,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _aheadPrefetchConcurrency.Release();
        }
    }

    /// <summary>
    /// Inicia uma única tarefa de pré-cache por mídia. Ela não fica presa à requisição HTTP
    /// que criou o stream e, portanto, continua baixando as partes mesmo quando o cliente
    /// troca de range/segmento durante a reprodução.
    /// </summary>
    public void StartPrefetch(string mediaKey, Func<CancellationToken, Task> prefetch)
    {
        ArgumentNullException.ThrowIfNull(prefetch);
        var normalized = NormalizeMediaKey(mediaKey);
        PrefetchState state;
        _storageGate.Wait();
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            _cancelWhenIdle.TryRemove(normalized, out _);
            if (_prefetches.TryGetValue(normalized, out var currentPrefetch))
            {
                if (!currentPrefetch.IsCancellationRequested)
                {
                    return;
                }

                _prefetches.TryRemove(new KeyValuePair<string, PrefetchState>(normalized, currentPrefetch));
            }

            if (_allPrefetches.Count >= MaxConcurrentMediaPrefetches + MaxQueuedMediaPrefetches)
            {
                _logger.LogDebug(
                    "[NEBULA-CACHE] Pré-cache integral ignorado por limite de admissão; playback sob demanda permanece disponível para {MediaKey}.",
                    normalized);
                return;
            }

            state = new PrefetchState(_logger);
            if (!_prefetches.TryAdd(normalized, state))
            {
                state.Dispose();
                return;
            }

            _allPrefetches.TryAdd(state, 0);
        }
        finally
        {
            _storageGate.Release();
        }

        _ = RunPrefetchAsync(normalized, state, prefetch);
    }

    /// <summary>Requests cancellation of a media prefetch after the playback session ends.</summary>
    public bool CancelPrefetch(string mediaKey)
    {
        var normalized = NormalizeMediaKey(mediaKey);
        PrefetchState? stateToCancel = null;
        _storageGate.Wait();
        try
        {
            if (Volatile.Read(ref _disposed) != 0 || !_prefetches.TryGetValue(normalized, out var state))
            {
                return false;
            }

            if (_activeMedia.ContainsKey(normalized))
            {
                _cancelWhenIdle[normalized] = 0;
                return true;
            }

            state.MarkCancellationRequested();
            stateToCancel = state;
        }
        finally
        {
            _storageGate.Release();
        }

        stateToCancel.Cancel();
        return true;
    }

    internal void CleanupExpiredEntries(DateTime? nowUtc = null)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (!_storageGate.Wait(0))
        {
            return;
        }

        var cutoff = (nowUtc ?? DateTime.UtcNow) - _entryLifetime;
        try
        {
            if (!Directory.Exists(_rootPath))
            {
                return;
            }

            foreach (var directory in Directory.EnumerateDirectories(_rootPath))
            {
                try
                {
                    var mediaKey = Path.GetFileName(directory);
                    if (_activeMedia.ContainsKey(mediaKey))
                    {
                        continue;
                    }

                    if (_prefetches.ContainsKey(mediaKey))
                    {
                        continue;
                    }

                    if (_inflight.Keys.Any(k => k.StartsWith(mediaKey + ":", StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    var lastActivity = _lastActivity.TryGetValue(mediaKey, out var tracked)
                        ? tracked
                        : GetDirectoryLastActivityUtc(directory);
                    if (lastActivity > cutoff)
                    {
                        continue;
                    }

                    DeleteDirectorySafe(directory);
                    _lastActivity.TryRemove(mediaKey, out _);
                    _logger.LogInformation("[NEBULA-CACHE] Cache de reprodução expirado (> 1 hora sem uso) removido: {MediaKey}", mediaKey);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogDebug(ex, "[NEBULA-CACHE] Não foi possível remover o diretório de cache {Directory}.", directory);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "[NEBULA-CACHE] Não foi possível concluir a enumeração do cache de reprodução.");
        }
        finally
        {
            _storageGate.Release();
        }

        EnforceConfiguredLimits();
    }

    private void EnforceConfiguredLimits()
    {
        if (!_storageGate.Wait(0))
        {
            return;
        }

        try
        {
            TrimConfiguredLimits();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "[NEBULA-CACHE] Não foi possível impor os limites durante a limpeza periódica.");
        }
        finally
        {
            _storageGate.Release();
        }
    }

    private void TrimConfiguredLimits()
    {
        if (!TryGetCacheSizeBytes(out var cacheSize))
        {
            return;
        }

        var availableSpace = GetAvailableFreeSpaceBytes();
        var attemptedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (cacheSize > MaxCacheBytes
            || (MinimumFreeSpaceBytes > 0 && availableSpace.HasValue && availableSpace.Value < MinimumFreeSpaceBytes))
        {
            var oldestDirectory = Directory.GetDirectories(_rootPath)
                .Select(directory => new
                {
                    Directory = directory,
                    Key = Path.GetFileName(directory),
                    LastActivity = _lastActivity.TryGetValue(Path.GetFileName(directory), out var activity)
                        ? activity
                        : GetDirectoryLastActivityUtc(directory)
                })
                .Where(entry => !_activeMedia.ContainsKey(entry.Key)
                    && !attemptedDirectories.Contains(entry.Directory)
                    && !_prefetches.ContainsKey(entry.Key)
                    && !_inflight.Keys.Any(key => key.StartsWith(entry.Key + ":", StringComparison.Ordinal)))
                .OrderBy(entry => entry.LastActivity)
                .FirstOrDefault();

            if (oldestDirectory is null)
            {
                return;
            }

            attemptedDirectories.Add(oldestDirectory.Directory);
            DeleteDirectorySafe(oldestDirectory.Directory);
            _lastActivity.TryRemove(oldestDirectory.Key, out _);
            if (!TryGetCacheSizeBytes(out cacheSize))
            {
                return;
            }

            availableSpace = GetAvailableFreeSpaceBytes();
        }
    }

    private static DateTime GetDirectoryLastActivityUtc(string directory)
    {
        var latest = Directory.GetLastWriteTimeUtc(directory);
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                var fileTime = File.GetLastWriteTimeUtc(file);
                if (fileTime > latest)
                {
                    latest = fileTime;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return latest;
    }

    private static void DeleteDirectorySafe(string directory)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            Directory.Delete(directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private async Task<byte[]> DownloadAndPersistAsync(
        string path,
        int expectedLength,
        Func<CancellationToken, Task<byte[]>> fetch,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _telegramFetchCount);
        byte[] data;
        try
        {
            data = await fetch(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Interlocked.Increment(ref _telegramFetchFailures);
            throw;
        }
        finally
        {
            Interlocked.Add(ref _telegramFetchDurationTicks, Stopwatch.GetElapsedTime(startedAt).Ticks);
        }
        if (data.Length == 0)
        {
            return data;
        }

        await _storageGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var temporaryPath = path + ".partial";
        try
        {
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            if (!EnsureCapacityFor(normalizedMediaKey: Path.GetFileName(directory), path, data.Length))
            {
                _logger.LogDebug("[NEBULA-CACHE] Cache/reserva atingida; bloco será transmitido sem persistência: {Path}", path);
                return data;
            }

            await File.WriteAllBytesAsync(temporaryPath, data, cancellationToken).ConfigureAwait(false);
            if (data.Length >= expectedLength)
            {
                File.Move(temporaryPath, path, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Interlocked.Increment(ref _cacheErrors);
            _logger.LogWarning(ex, "[NEBULA-CACHE] Não foi possível persistir bloco; mantendo reprodução sem cache: {Path}", path);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

            _storageGate.Release();
        }

        return data;
    }

    private bool EnsureCapacityFor(string normalizedMediaKey, string path, long incomingBytes)
    {
        if (!TryGetCacheSizeBytes(out var cacheSize))
        {
            return false;
        }

        // Count the temporary write alongside the old file so both quota and reserve hold during atomic replacement.
        var requiredSize = AddWithoutOverflow(cacheSize, incomingBytes);
        var availableSpace = GetAvailableFreeSpaceBytes();
        if (MinimumFreeSpaceBytes > 0 && !availableSpace.HasValue)
        {
            return false;
        }

        var attemptedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (requiredSize > MaxCacheBytes || (availableSpace.HasValue && availableSpace.Value - incomingBytes < MinimumFreeSpaceBytes))
        {
            var oldestDirectory = Directory.GetDirectories(_rootPath)
                .Select(directory => new
                {
                    Directory = directory,
                    Key = Path.GetFileName(directory),
                    LastActivity = _lastActivity.TryGetValue(Path.GetFileName(directory), out var activity)
                        ? activity
                        : GetDirectoryLastActivityUtc(directory)
                })
                .Where(entry => entry.Key != normalizedMediaKey
                    && !attemptedDirectories.Contains(entry.Directory)
                    && !_activeMedia.ContainsKey(entry.Key)
                    && !_prefetches.ContainsKey(entry.Key)
                    && !_inflight.Keys.Any(key => key.StartsWith(entry.Key + ":", StringComparison.Ordinal)))
                .OrderBy(entry => entry.LastActivity)
                .FirstOrDefault();

            if (oldestDirectory is null)
            {
                return false;
            }

            attemptedDirectories.Add(oldestDirectory.Directory);
            DeleteDirectorySafe(oldestDirectory.Directory);
            _lastActivity.TryRemove(oldestDirectory.Key, out _);
            if (!TryGetCacheSizeBytes(out cacheSize))
            {
                return false;
            }

            requiredSize = AddWithoutOverflow(cacheSize, incomingBytes);
            availableSpace = GetAvailableFreeSpaceBytes();
        }

        return true;
    }

    private static long AddWithoutOverflow(long left, long right)
    {
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }

    private long? GetAvailableFreeSpaceBytes()
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(_rootPath));
            if (string.IsNullOrEmpty(root))
            {
                return null;
            }

            var drive = new DriveInfo(root);
            return drive.IsReady ? drive.AvailableFreeSpace : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private async Task RemoveCompletedFetchAsync(string inflightKey, SharedFetch fetch, Task<byte[]> sharedTask)
    {
        try
        {
            await sharedTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-CACHE] Download compartilhado do bloco falhou: {ChunkKey}", inflightKey);
        }
        finally
        {
            ((ICollection<KeyValuePair<string, SharedFetch>>)_inflight)
                .Remove(new KeyValuePair<string, SharedFetch>(inflightKey, fetch));
            fetch.Dispose();
        }
    }

    private bool TryReadComplete(string path, int expectedLength, out byte[] data)
    {
        data = Array.Empty<byte>();
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length < expectedLength)
            {
                return false;
            }

            data = File.ReadAllBytes(path);
            return data.Length >= expectedLength;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void Touch(string mediaKey)
    {
        _lastActivity[mediaKey] = DateTime.UtcNow;
        try
        {
            Directory.CreateDirectory(GetMediaDirectory(mediaKey));
        }
        catch (IOException)
        {
        }
    }

    private string GetMediaDirectory(string mediaKey)
    {
        return Path.Combine(_rootPath, mediaKey);
    }

    private static string NormalizeMediaKey(string mediaKey)
    {
        var value = string.IsNullOrWhiteSpace(mediaKey) ? "unknown-media" : mediaKey.Trim();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private void Release(string mediaKey)
    {
        PrefetchState? state = null;
        var cancelImmediately = false;
        _storageGate.Wait();
        try
        {
            if (!_activeMedia.TryGetValue(mediaKey, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _activeMedia.TryRemove(mediaKey, out _);
            }
            else
            {
                _activeMedia[mediaKey] = count - 1;
            }

            if (Volatile.Read(ref _disposed) == 0)
            {
                Touch(mediaKey);
                if (!_activeMedia.ContainsKey(mediaKey))
                {
                    if (_cancelWhenIdle.TryRemove(mediaKey, out _))
                    {
                        _prefetches.TryGetValue(mediaKey, out state);
                        cancelImmediately = true;
                    }
                    else
                    {
                        _prefetches.TryGetValue(mediaKey, out state);
                    }
                }
            }
        }
        finally
        {
            _storageGate.Release();
        }

        if (state is not null)
        {
            if (cancelImmediately)
            {
                CancelPrefetchIfIdle(mediaKey, state);
            }
            else
            {
                _ = CancelPrefetchWhenIdleAsync(mediaKey, state);
            }
        }
    }

    private void CancelPrefetchIfIdle(string mediaKey, PrefetchState expectedState)
    {
        var cancel = false;
        _storageGate.Wait();
        try
        {
            if (Volatile.Read(ref _disposed) == 0
                && !_activeMedia.ContainsKey(mediaKey)
                && _prefetches.TryGetValue(mediaKey, out var current)
                && ReferenceEquals(current, expectedState))
            {
                expectedState.MarkCancellationRequested();
                cancel = true;
            }
        }
        finally
        {
            _storageGate.Release();
        }

        if (cancel)
        {
            expectedState.Cancel();
        }
    }

    private async Task RunPrefetchAsync(string mediaKey, PrefetchState state, Func<CancellationToken, Task> prefetch)
    {
        var acquiredConcurrencySlot = false;
        try
        {
            if (Volatile.Read(ref _disposed) == 0 && !state.IsCancellationRequested)
            {
                await _prefetchConcurrency.WaitAsync(state.Token).ConfigureAwait(false);
                acquiredConcurrencySlot = true;
                state.MarkConcurrencySlotAcquired();
                if (Volatile.Read(ref _disposed) == 0 && !state.IsCancellationRequested)
                {
                    await prefetch(state.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (state.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-CACHE] Pré-cache da mídia {MediaKey} interrompido.", mediaKey);
        }
        finally
        {
            _prefetches.TryRemove(new KeyValuePair<string, PrefetchState>(mediaKey, state));
            _allPrefetches.TryRemove(state, out _);
            state.Dispose();
            if (acquiredConcurrencySlot)
            {
                _prefetchConcurrency.Release();
            }
        }
    }

    private async Task CancelPrefetchWhenIdleAsync(string mediaKey, PrefetchState state)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false);
            await _storageGate.WaitAsync().ConfigureAwait(false);
            var cancel = false;
            try
            {
                if (Volatile.Read(ref _disposed) == 0
                    && !_activeMedia.ContainsKey(mediaKey)
                    && _prefetches.TryGetValue(mediaKey, out var current)
                    && ReferenceEquals(current, state))
                {
                    state.MarkCancellationRequested();
                    cancel = true;
                }
            }
            finally
            {
                _storageGate.Release();
            }

            if (cancel)
            {
                state.Cancel();
            }
        }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        (SharedFetch[] Fetches, PrefetchState[] Prefetches)? resources;
        _storageGate.Wait();
        try
        {
            resources = BeginDisposeUnderGate(onlyWhenIdle: false);
        }
        finally
        {
            _storageGate.Release();
        }

        if (resources.HasValue)
        {
            FinishDispose(resources.Value);
        }
    }

    private (SharedFetch[] Fetches, PrefetchState[] Prefetches)? BeginDisposeUnderGate(bool onlyWhenIdle)
    {
        if (Volatile.Read(ref _disposed) != 0
            || (onlyWhenIdle && (_activeMedia.Count != 0 || _inflight.Count != 0 || _allPrefetches.Count != 0)))
        {
            return null;
        }

        Interlocked.Exchange(ref _disposed, 1);
        _cleanupTimer.Dispose();
        var fetches = _inflight.Values.ToArray();
        var prefetches = _allPrefetches.Keys.ToArray();
        _inflight.Clear();
        _prefetches.Clear();
        _allPrefetches.Clear();
        _activeMedia.Clear();
        _cancelWhenIdle.Clear();
        _lastActivity.Clear();
        return (fetches, prefetches);
    }

    private static void FinishDispose((SharedFetch[] Fetches, PrefetchState[] Prefetches) resources)
    {
        foreach (var fetch in resources.Fetches)
        {
            fetch.Cancel();
            fetch.Dispose();
        }
        foreach (var state in resources.Prefetches)
        {
            state.MarkCancellationRequested();
            state.Cancel();
        }
    }

    private sealed class PlaybackLease : IDisposable
    {
        private readonly NebulaPlaybackCache _owner;
        private readonly string _mediaKey;
        private int _released;

        public PlaybackLease(NebulaPlaybackCache owner, string mediaKey)
        {
            _owner = owner;
            _mediaKey = mediaKey;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                _owner.Release(_mediaKey);
            }
        }
    }

    private sealed class PrefetchState : IDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly ILogger _logger;
        private bool _disposed;
        private bool _disposeRequested;
        private Task? _cancelCallbacksTask;
        private int _cancellationRequested;
        private int _cancelSignaled;
        private int _hasConcurrencySlot;

        public CancellationToken Token { get; }

        public PrefetchState(ILogger logger)
        {
            _logger = logger;
            Token = _cancellation.Token;
        }

        public bool IsCancellationRequested => Volatile.Read(ref _cancellationRequested) != 0;

        public bool HasConcurrencySlot => Volatile.Read(ref _hasConcurrencySlot) != 0;

        public void MarkConcurrencySlotAcquired() => Volatile.Write(ref _hasConcurrencySlot, 1);

        public void MarkCancellationRequested()
        {
            lock (_gate)
            {
                if (!_disposed && _cancelSignaled == 0)
                {
                    Volatile.Write(ref _cancellationRequested, 1);
                }
            }
        }

        public void TryRevokeCancellationRequest()
        {
            lock (_gate)
            {
                if (!_disposed && _cancelSignaled == 0)
                {
                    Volatile.Write(ref _cancellationRequested, 0);
                }
            }
        }

        public void Cancel()
        {
            lock (_gate)
            {
                if (_disposed || _cancelSignaled != 0 || !IsCancellationRequested)
                {
                    return;
                }

                _cancelSignaled = 1;
                try
                {
                    // Callbacks can wait for the prefetch task to finish. Run them asynchronously
                    // so the task can leave its finally block and request disposal without a cycle.
                    _cancelCallbacksTask = _cancellation.CancelAsync();
                }
                catch (ObjectDisposedException)
                {
                    // The prefetch can finish naturally and dispose its state after the request is marked.
                }
            }
        }

        public void Dispose()
        {
            Task? cancelCallbacksTask;
            lock (_gate)
            {
                if (_disposed || _disposeRequested)
                {
                    return;
                }

                _disposeRequested = true;
                cancelCallbacksTask = _cancelCallbacksTask;
                if (cancelCallbacksTask is null || cancelCallbacksTask.IsCompleted)
                {
                    DisposeCancellationSource(cancelCallbacksTask);
                    return;
                }
            }

            _ = DisposeAfterCancellationCallbacksAsync(cancelCallbacksTask);
        }

        private async Task DisposeAfterCancellationCallbacksAsync(Task cancelCallbacksTask)
        {
            try
            {
                await cancelCallbacksTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // DisposeCancellationSource inspeciona e registra a falha uma única vez.
            }

            lock (_gate)
            {
                DisposeCancellationSource(cancelCallbacksTask);
            }
        }

        private void DisposeCancellationSource(Task? cancelCallbacksTask)
        {
            if (_disposed)
            {
                return;
            }

            if (cancelCallbacksTask?.Exception is { } callbackError)
            {
                _logger.LogWarning(callbackError, "Falha em callback ao cancelar uma tarefa de pré-cache Nebula.");
            }

            _disposed = true;
            _cancellation.Dispose();
        }
    }

    private sealed class SharedFetch : IDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Func<CancellationToken, Task<byte[]>> _fetch;
        private readonly ILogger _logger;
        private Task<byte[]>? _task;
        private Task? _cancelCallbacksTask;
        private int _waiters;
        private bool _disposed;
        private bool _disposeRequested;

        public SharedFetch(Func<CancellationToken, Task<byte[]>> fetch, ILogger logger)
        {
            _fetch = fetch;
            _logger = logger;
        }

        public bool TryJoin(out Task<byte[]> task)
        {
            lock (_gate)
            {
                if (_disposed || _cancellation.IsCancellationRequested || _task?.IsCompleted == true)
                {
                    task = Task.FromCanceled<byte[]>(new CancellationToken(true));
                    return false;
                }

                _waiters++;
                _task ??= StartFetch();
                task = _task;
                return true;
            }
        }

        public void Leave()
        {
            lock (_gate)
            {
                _waiters = Math.Max(0, _waiters - 1);
                if (!_disposed && _waiters == 0 && _task?.IsCompleted == false)
                {
                    RequestCancellationUnderGate();
                }
            }
        }

        public void Cancel()
        {
            lock (_gate)
            {
                if (!_disposed && !_cancellation.IsCancellationRequested)
                {
                    RequestCancellationUnderGate();
                }
            }
        }

        private Task<byte[]> StartFetch()
        {
            try
            {
                return _fetch(_cancellation.Token);
            }
            catch (Exception ex)
            {
                return Task.FromException<byte[]>(ex);
            }
        }

        public void Dispose()
        {
            Task? cancelCallbacksTask;
            lock (_gate)
            {
                if (_disposeRequested)
                {
                    return;
                }

                _disposeRequested = true;
                if (_task?.IsCompleted == false && !_cancellation.IsCancellationRequested)
                {
                    RequestCancellationUnderGate();
                }

                cancelCallbacksTask = _cancelCallbacksTask;
                if (cancelCallbacksTask is null || cancelCallbacksTask.IsCompleted)
                {
                    DisposeCancellationSource(cancelCallbacksTask);
                    return;
                }
            }

            _ = DisposeAfterCancellationCallbacksAsync(cancelCallbacksTask);
        }

        private void RequestCancellationUnderGate()
        {
            if (!_cancellation.IsCancellationRequested)
            {
                _cancelCallbacksTask = _cancellation.CancelAsync();
            }
        }

        private async Task DisposeAfterCancellationCallbacksAsync(Task cancelCallbacksTask)
        {
            try
            {
                await cancelCallbacksTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // DisposeCancellationSource registra a falha uma única vez.
            }

            lock (_gate)
            {
                DisposeCancellationSource(cancelCallbacksTask);
            }
        }

        private void DisposeCancellationSource(Task? cancelCallbacksTask)
        {
            if (_disposed)
            {
                return;
            }

            if (cancelCallbacksTask?.Exception is { } callbackError)
            {
                _logger.LogWarning(callbackError, "Falha em callback ao cancelar um download compartilhado Nebula.");
            }

            _disposed = true;
            _cancellation.Dispose();
        }
    }
}
