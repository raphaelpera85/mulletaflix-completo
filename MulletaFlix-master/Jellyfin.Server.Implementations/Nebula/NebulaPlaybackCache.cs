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
/// O cache é separado por mídia e expira uma hora depois da última atividade.
/// </summary>
public sealed class NebulaPlaybackCache : IDisposable
{
    private static readonly TimeSpan EntryLifetime = TimeSpan.FromHours(1);
    private readonly string _rootPath;
    private readonly ILogger<NebulaPlaybackCache> _logger;
    private readonly ConcurrentDictionary<string, int> _activeMedia = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _lastActivity = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> _inflight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PrefetchState> _prefetches = new(StringComparer.Ordinal);
    private readonly Timer _cleanupTimer;
    private int _disposed;

    public NebulaPlaybackCache(string cachePath, ILogger<NebulaPlaybackCache> logger)
    {
        if (string.IsNullOrWhiteSpace(cachePath))
        {
            throw new ArgumentException("O caminho do cache de reprodução é obrigatório.", nameof(cachePath));
        }

        _rootPath = Path.Combine(cachePath, "nebula-playback");
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Directory.CreateDirectory(_rootPath);
        _cleanupTimer = new Timer(static state => ((NebulaPlaybackCache)state!).CleanupExpiredEntries(), this, EntryLifetime, EntryLifetime);
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
        var lazy = _inflight.GetOrAdd(inflightKey, _ => new Lazy<Task<byte[]>>(
            () => DownloadAndPersistAsync(path, expectedLength, fetch, cancellationToken),
            LazyThreadSafetyMode.ExecutionAndPublication));
        try
        {
            return await lazy.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _inflight.TryRemove(inflightKey, out _);
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

        var cutoff = (nowUtc ?? DateTime.UtcNow) - EntryLifetime;
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(_rootPath))
            {
                var mediaKey = Path.GetFileName(directory);
                if (_activeMedia.ContainsKey(mediaKey))
                {
                    continue;
                }

                var lastActivity = _lastActivity.TryGetValue(mediaKey, out var tracked)
                    ? tracked
                    : Directory.GetLastWriteTimeUtc(directory);
                if (lastActivity > cutoff)
                {
                    continue;
                }

                Directory.Delete(directory, recursive: true);
                _lastActivity.TryRemove(mediaKey, out _);
                _logger.LogInformation("[NEBULA-CACHE] Cache de reprodução expirado removido: {MediaKey}", mediaKey);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "[NEBULA-CACHE] Não foi possível concluir a limpeza do cache de reprodução.");
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

        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = path + ".partial";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, data, cancellationToken).ConfigureAwait(false);
            if (data.Length >= expectedLength)
            {
                File.Move(temporaryPath, path, overwrite: true);
            }
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
            catch (IOException)
            {
            }
        }

        return data;
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
}
