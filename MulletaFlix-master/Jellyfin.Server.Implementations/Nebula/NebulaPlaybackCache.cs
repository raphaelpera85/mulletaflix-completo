using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    private readonly string _rootPath;
    private readonly ILogger<NebulaPlaybackCache> _logger;
    private readonly TimeSpan _entryLifetime;
    private readonly long _maxCacheBytes;
    private readonly long _minimumFreeSpaceBytes;
    private readonly SemaphoreSlim _storageGate = new(1, 1);
    private readonly ConcurrentDictionary<string, int> _activeMedia = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _lastActivity = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SharedFetch> _inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PrefetchState> _prefetches = new(StringComparer.Ordinal);
    private readonly Timer _cleanupTimer;
    private int _disposed;

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

    public long MaxCacheBytes => _maxCacheBytes;

    public long MinimumFreeSpaceBytes => _minimumFreeSpaceBytes;

    /// <summary>
    /// Retorna o tamanho total ocupado em bytes pelo cache no disco.
    /// </summary>
    public long GetCacheSizeBytes()
    {
        try
        {
            if (!Directory.Exists(_rootPath))
            {
                return 0;
            }

            var dirInfo = new DirectoryInfo(_rootPath);
            return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(static f => f.Length);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-CACHE] Falha ao calcular tamanho do cache.");
            return 0;
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
    public void ClearCache()
    {
        try
        {
            if (!Directory.Exists(_rootPath))
            {
                return;
            }

            foreach (var directory in Directory.GetDirectories(_rootPath))
            {
                var mediaKey = Path.GetFileName(directory);
                if (_activeMedia.ContainsKey(mediaKey) || _prefetches.ContainsKey(mediaKey))
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
                    _logger.LogWarning(ex, "[NEBULA-CACHE] Falha ao limpar diretório de mídia {Directory}.", directory);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-CACHE] Falha ao limpar cache geral.");
        }
    }

    /// <summary>Marca uma mídia como em reprodução. Enquanto houver uma sessão, seus blocos não são removidos.</summary>
    public IDisposable Acquire(string mediaKey)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var normalized = NormalizeMediaKey(mediaKey);
        _activeMedia.AddOrUpdate(normalized, 1, static (_, count) => count + 1);
        Touch(normalized);
        return new PlaybackLease(this, normalized);
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
            return cached;
        }

        var inflightKey = $"{normalized}:{partIndex}:{chunkIndex}";
        while (true)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            var candidate = new SharedFetch(token => DownloadAndPersistAsync(path, expectedLength, fetch, token));
            var shared = _inflight.GetOrAdd(inflightKey, candidate);
            if (!shared.TryJoin(out var sharedTask))
            {
                ((ICollection<KeyValuePair<string, SharedFetch>>)_inflight)
                    .Remove(new KeyValuePair<string, SharedFetch>(inflightKey, shared));
                continue;
            }

            if (ReferenceEquals(shared, candidate))
            {
                _ = RemoveCompletedFetchAsync(inflightKey, shared, sharedTask);
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

    /// <summary>
    /// Inicia uma única tarefa de pré-cache por mídia. Ela não fica presa à requisição HTTP
    /// que criou o stream e, portanto, continua baixando as partes mesmo quando o cliente
    /// troca de range/segmento durante a reprodução.
    /// </summary>
    public void StartPrefetch(string mediaKey, Func<CancellationToken, Task> prefetch)
    {
        ArgumentNullException.ThrowIfNull(prefetch);
        var normalized = NormalizeMediaKey(mediaKey);
        if (_prefetches.ContainsKey(normalized))
        {
            return;
        }

        var state = new PrefetchState(new CancellationTokenSource());
        if (!_prefetches.TryAdd(normalized, state))
        {
            state.Cancellation.Dispose();
            return;
        }

        _ = RunPrefetchAsync(normalized, state, prefetch);
    }

    internal void CleanupExpiredEntries(DateTime? nowUtc = null)
    {
        if (Volatile.Read(ref _disposed) != 0)
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
        var data = await fetch(cancellationToken).ConfigureAwait(false);
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
        var existingLength = File.Exists(path) ? new FileInfo(path).Length : 0;
        var requiredSize = Math.Max(0, GetCacheSizeBytes() - existingLength) + incomingBytes;
        var availableSpace = GetAvailableFreeSpaceBytes();
        while (requiredSize > _maxCacheBytes || (availableSpace.HasValue && availableSpace.Value - incomingBytes < _minimumFreeSpaceBytes))
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
                    && !_activeMedia.ContainsKey(entry.Key)
                    && !_prefetches.ContainsKey(entry.Key)
                    && !_inflight.Keys.Any(key => key.StartsWith(entry.Key + ":", StringComparison.Ordinal)))
                .OrderBy(entry => entry.LastActivity)
                .FirstOrDefault();

            if (oldestDirectory is null)
            {
                return false;
            }

            var removedBytes = Directory.EnumerateFiles(oldestDirectory.Directory, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
            DeleteDirectorySafe(oldestDirectory.Directory);
            _lastActivity.TryRemove(oldestDirectory.Key, out _);
            requiredSize = Math.Max(0, requiredSize - removedBytes);
            availableSpace = GetAvailableFreeSpaceBytes();
        }

        return true;
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
        _activeMedia.AddOrUpdate(mediaKey, 0, static (_, count) => Math.Max(0, count - 1));
        if (_activeMedia.TryGetValue(mediaKey, out var count) && count == 0)
        {
            _activeMedia.TryRemove(mediaKey, out _);
        }

        Touch(mediaKey);
        if (!_activeMedia.ContainsKey(mediaKey) && _prefetches.TryGetValue(mediaKey, out var state))
        {
            _ = CancelPrefetchWhenIdleAsync(mediaKey, state);
        }
    }

    private async Task RunPrefetchAsync(string mediaKey, PrefetchState state, Func<CancellationToken, Task> prefetch)
    {
        try
        {
            await prefetch(state.Cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (state.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-CACHE] Pré-cache da mídia {MediaKey} interrompido.", mediaKey);
        }
        finally
        {
            _prefetches.TryRemove(new KeyValuePair<string, PrefetchState>(mediaKey, state));
            state.Cancellation.Dispose();
        }
    }

    private async Task CancelPrefetchWhenIdleAsync(string mediaKey, PrefetchState state)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2)).ConfigureAwait(false);
            if (!_activeMedia.ContainsKey(mediaKey))
            {
                state.Cancellation.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cleanupTimer.Dispose();
        foreach (var fetch in _inflight.Values)
        {
            fetch.Cancel();
        }
        _inflight.Clear();
        foreach (var state in _prefetches.Values)
        {
            state.Cancellation.Cancel();
            state.Cancellation.Dispose();
        }
        _prefetches.Clear();
        _activeMedia.Clear();
        _lastActivity.Clear();
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

    private sealed class PrefetchState
    {
        public PrefetchState(CancellationTokenSource cancellation)
        {
            Cancellation = cancellation;
        }

        public CancellationTokenSource Cancellation { get; }
    }

    private sealed class SharedFetch : IDisposable
    {
        private readonly object _gate = new();
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Func<CancellationToken, Task<byte[]>> _fetch;
        private Task<byte[]>? _task;
        private int _waiters;

        public SharedFetch(Func<CancellationToken, Task<byte[]>> fetch) => _fetch = fetch;

        public bool TryJoin(out Task<byte[]> task)
        {
            lock (_gate)
            {
                if (_cancellation.IsCancellationRequested || _task?.IsCompleted == true)
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
                if (_waiters == 0 && _task?.IsCompleted == false)
                {
                    _cancellation.Cancel();
                }
            }
        }

        public void Cancel()
        {
            lock (_gate)
            {
                if (!_cancellation.IsCancellationRequested)
                {
                    _cancellation.Cancel();
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

        public void Dispose() => _cancellation.Dispose();
    }
}
