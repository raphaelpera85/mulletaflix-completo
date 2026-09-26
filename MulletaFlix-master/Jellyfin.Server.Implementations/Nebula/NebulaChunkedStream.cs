using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Representa os metadados de uma parte de mídia armazenada no Telegram.
/// </summary>
public sealed class NebulaStreamPart
{
    /// <summary>
    /// Gets or sets o índice sequencial da parte.
    /// </summary>
    public int PartIndex { get; set; }

    /// <summary>
    /// Gets or sets o deslocamento em bytes da parte dentro do arquivo consolidado.
    /// </summary>
    public long FileOffset { get; set; }

    /// <summary>
    /// Gets or sets o tamanho em bytes desta parte.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets o identificador do arquivo no Telegram (Pyrogram/Bot API).
    /// </summary>
    public string? FileId { get; set; }

    /// <summary>
    /// Gets or sets o índice do bot responsável pelo upload original.
    /// </summary>
    public int BotIndex { get; set; }

    /// <summary>
    /// Gets or sets o ID do canal onde a mensagem foi publicada.
    /// </summary>
    public long ChatId { get; set; }

    /// <summary>
    /// Gets or sets o ID da mensagem no canal do Telegram.
    /// </summary>
    public int MessageId { get; set; }

    /// <summary>
    /// Gets or sets o caminho local em disco caso o arquivo ainda esteja em staging ou cache local.
    /// </summary>
    public string? LocalPath { get; set; }
}

/// <summary>
/// Stream de leitura sob demanda (chunked) para mídias hospedadas em partes no Telegram via Nebula.
/// Permite seek instantâneo e streaming de blocos de 1 MB sob demanda com cache LRU em memória,
/// garantindo inicialização de reprodução imediata sem bufferizar gigabytes inteiros em RAM.
/// </summary>
public sealed class NebulaChunkedStream : Stream
{
    private const int ChunkSize = 1024 * 1024; // 1 MB por bloco (idêntico ao GETFILE_CHUNK_SIZE do Nebula Python)

    // Anteriormente 64, ou seja 64 MB de Large Object Heap *por stream ativo*. Como o
    // NebulaHttpStreamServer aceita até 32 conexões simultâneas, isso podia chegar perto de 2 GB, e
    // no p95 medido (12 concorrentes) ficava em ~768 MB. Em um host com ~1,9 GB de RAM livre e 3,9 GB
    // de pagefile em uso, esse era um dos maiores contribuintes dos congelamentos de 50-100 s
    // observados no log (o processo ficava dezenas de segundos sem executar nada enquanto paginava).
    // 8 blocos ainda dão 4x de folga sobre o PrefetchAheadChunks de 2, cobrindo prefetch e seek
    // sequencial, com 1/8 da memória. O custo aceito é que um scrub longo pode precisar rebaixar
    // blocos do Telegram em vez de achá-los no LRU — troca deliberada de banda por memória, porque a
    // banda já era o recurso abundante (uploads a 5-6 MB/s) e a memória é o recurso que faltava.
    private const int MaxCachedChunks = 8;
    private const int PrefetchAheadChunks = 2; // Número de chunks à frente para pré-carregar em segundo plano

    private readonly NebulaTelegramPool? _telegramPool;
    private readonly IReadOnlyList<NebulaStreamPart> _parts;
    private readonly long _totalLength;
    private readonly ILogger _logger;
    private readonly NebulaPlaybackCache? _playbackCache;
    private readonly string _mediaKey;
    private readonly IDisposable? _playbackLease;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly CancellationTokenSource _streamCts = new();

    private readonly Dictionary<string, byte[]> _chunkCache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lruOrder = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<byte[]>> _inflightPrefetches = new(StringComparer.Ordinal);

    private long _position;
    private bool _disposed;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaChunkedStream"/>.
    /// </summary>
    public NebulaChunkedStream(
        NebulaTelegramPool? telegramPool,
        IEnumerable<NebulaStreamPart> parts,
        long? totalLength,
        ILogger logger,
        NebulaPlaybackCache? playbackCache = null,
        string? mediaKey = null)
    {
        _telegramPool = telegramPool;
        _parts = parts.OrderBy(p => p.PartIndex).ToList();
        _logger = logger;
        _playbackCache = playbackCache;
        _mediaKey = string.IsNullOrWhiteSpace(mediaKey) ? "unknown-media" : mediaKey;
        _playbackLease = _playbackCache?.Acquire(_mediaKey);

        if (_parts.Count > 0)
        {
            var calculatedLength = _parts[^1].FileOffset + _parts[^1].Size;
            _totalLength = totalLength.HasValue && totalLength.Value > 0 ? totalLength.Value : calculatedLength;
        }
        else
        {
            _totalLength = totalLength ?? 0;
        }

        if (_playbackCache != null && _parts.Count > 0)
        {
            _playbackCache.StartPrefetch(_mediaKey, PrefetchWholeMediaAsync);
        }
    }

    /// <inheritdoc />
    public override bool CanRead => !_disposed;

    /// <inheritdoc />
    public override bool CanSeek => !_disposed;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length
    {
        get
        {
            EnsureNotDisposed();
            return _totalLength;
        }
    }

    /// <inheritdoc />
    public override long Position
    {
        get
        {
            EnsureNotDisposed();
            return _position;
        }
        set
        {
            EnsureNotDisposed();
            Seek(value, SeekOrigin.Begin);
        }
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureNotDisposed();
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _totalLength + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (target < 0)
        {
            throw new IOException("Tentativa de posicionar o stream antes do início do arquivo.");
        }

        _position = Math.Min(target, _totalLength);
        return _position;
    }

    /// <inheritdoc />
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc />
    public override int Read(byte[] buffer, int offset, int count)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || buffer.Length - offset < count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        // Paths that stream Nebula media use ReadAsync/ReadAsync(Memory) directly (the HTTP
        // stream server and the downloader both read asynchronously). This synchronous override
        // only exists to satisfy consumers that insist on Stream.Read; it mirrors FileStream.Read
        // semantics (blocking until the chunk is fetched) instead of spinning a thread through
        // GetAwaiter().GetResult() on an already-running async state machine.
        if (_disposed || _position >= _totalLength || count == 0 || _parts.Count == 0)
        {
            return 0;
        }

        _lock.Wait();
        try
        {
            if (_disposed)
            {
                return 0;
            }

            var part = FindPartForPosition(_position);
            if (part is null)
            {
                throw new IOException($"Layout de partes Nebula inconsistente no offset {_position}.");
            }

            var offsetInPart = _position - part.FileOffset;
            var chunkIndex = (int)(offsetInPart / ChunkSize);
            var chunkOffset = (int)(offsetInPart % ChunkSize);

            // Fast path: serve from the LRU cache without touching the network.
            var cacheKey = $"{part.PartIndex}:{chunkIndex}";
            if (_chunkCache.TryGetValue(cacheKey, out var chunkData))
            {
                _lruOrder.Remove(cacheKey);
                _lruOrder.AddFirst(cacheKey);
                var availableInChunk = chunkData.Length - chunkOffset;
                var bytesToCopy = Math.Min(availableInChunk, count);
                if (bytesToCopy <= 0)
                {
                    return 0;
                }

                Buffer.BlockCopy(chunkData, chunkOffset, buffer, offset, bytesToCopy);
                _position += bytesToCopy;
                return bytesToCopy;
            }

            // Cache miss: perform a blocking fetch. This is the same behaviour as a synchronous
            // disk read and is not on the hot path of the streaming servers.
            var fetched = GetChunkAsync(part, chunkIndex, CancellationToken.None).GetAwaiter().GetResult();
            if (chunkOffset >= fetched.Length)
            {
                return 0;
            }

            var fetchedAvailable = fetched.Length - chunkOffset;
            var fetchedToCopy = Math.Min(fetchedAvailable, count);
            Buffer.BlockCopy(fetched, chunkOffset, buffer, offset, fetchedToCopy);
            _position += fetchedToCopy;
            return fetchedToCopy;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || buffer.Length - offset < count)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (_disposed || _position >= _totalLength || count == 0 || _parts.Count == 0)
        {
            return 0;
        }

        try
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }

        try
        {
            if (_disposed)
            {
                return 0;
            }

            var totalRead = 0;
            while (totalRead < count && _position < _totalLength && !cancellationToken.IsCancellationRequested)
            {
                var part = FindPartForPosition(_position);
                if (part == null)
                {
                    throw new IOException($"Layout de partes Nebula inconsistente no offset {_position}.");
                }

                var offsetInPart = _position - part.FileOffset;
                var chunkIndex = (int)(offsetInPart / ChunkSize);
                var chunkOffset = (int)(offsetInPart % ChunkSize);

                var chunkData = await GetChunkAsync(part, chunkIndex, cancellationToken).ConfigureAwait(false);
                if (chunkOffset >= chunkData.Length)
                {
                    break;
                }

                var availableInChunk = chunkData.Length - chunkOffset;
                var bytesToCopy = Math.Min(availableInChunk, count - totalRead);

                Buffer.BlockCopy(chunkData, chunkOffset, buffer, offset + totalRead, bytesToCopy);

                _position += bytesToCopy;
                totalRead += bytesToCopy;

                // Dispara pré-carregamento dos próximos chunks em background
                TriggerPrefetchAhead(part, chunkIndex);
            }

            return totalRead;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        finally
        {
            try
            {
                _lock.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    /// <inheritdoc />
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
        {
            return new ValueTask<int>(ReadAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));
        }

        // Non-array-backed Memory (rare): rent an array to avoid a synchronous fallback.
        var rented = ArrayPool<byte>.Shared.Rent(buffer.Length);
        return ReadAsyncRentedAsync(buffer, rented, cancellationToken);
    }

    private async ValueTask<int> ReadAsyncRentedAsync(Memory<byte> buffer, byte[] rented, CancellationToken cancellationToken)
    {
        try
        {
            var read = await ReadAsync(rented, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                rented.AsMemory(0, read).CopyTo(buffer);
            }

            return read;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private NebulaStreamPart? FindPartForPosition(long pos)
    {
        for (int i = 0; i < _parts.Count; i++)
        {
            var part = _parts[i];
            if (pos >= part.FileOffset && pos < part.FileOffset + part.Size)
            {
                return part;
            }
        }

        return null;
    }

    private void TriggerPrefetchAhead(NebulaStreamPart currentPart, int currentChunkIndex)
    {
        if (_disposed || _telegramPool == null)
        {
            return;
        }

        for (int step = 1; step <= PrefetchAheadChunks; step++)
        {
            var targetChunkIndex = currentChunkIndex + step;
            var targetOffset = (long)targetChunkIndex * ChunkSize;
            if (targetOffset < currentPart.Size)
            {
                var targetKey = $"{currentPart.PartIndex}:{targetChunkIndex}";
                if (!_chunkCache.ContainsKey(targetKey) && !_inflightPrefetches.ContainsKey(targetKey))
                {
                    var prefetchTask = Task.Run(async () =>
                    {
                        try
                        {
                            return await FetchChunkDataAsync(currentPart, targetChunkIndex, _streamCts.Token).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "[NEBULA-STREAM-PREFETCH] Falha ao pré-carregar chunk {Key}.", targetKey);
                            return Array.Empty<byte>();
                        }
                    });

                    _inflightPrefetches.TryAdd(targetKey, prefetchTask);
                }
            }
        }
    }

    private async Task<byte[]> GetChunkAsync(NebulaStreamPart part, int chunkIndex, CancellationToken cancellationToken)
    {
        var cacheKey = $"{part.PartIndex}:{chunkIndex}";
        if (_chunkCache.TryGetValue(cacheKey, out var cachedData))
        {
            _lruOrder.Remove(cacheKey);
            _lruOrder.AddFirst(cacheKey);
            return cachedData;
        }

        // Se houver um prefetch em andamento para este bloco, aguarda-o
        if (_inflightPrefetches.TryRemove(cacheKey, out var inflightTask))
        {
            try
            {
                var prefetched = await inflightTask.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (prefetched != null && prefetched.Length > 0)
                {
                    InsertIntoCache(cacheKey, prefetched);
                    return prefetched;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "[NEBULA-STREAM] Falha no prefetch de {Key}; prosseguindo com download direto.", cacheKey);
            }
        }

        var chunkData = _playbackCache == null
            ? await FetchChunkDataAsync(part, chunkIndex, cancellationToken).ConfigureAwait(false)
            : await _playbackCache.GetOrFetchChunkAsync(
                _mediaKey,
                part.PartIndex,
                chunkIndex,
                GetChunkLength(part, chunkIndex),
                token => FetchChunkDataAsync(part, chunkIndex, token),
                cancellationToken).ConfigureAwait(false);
        if (chunkData == null || chunkData.Length == 0)
        {
            _logger.LogError("[NEBULA-STREAM] Falha ao obter chunk {Key} do Telegram após múltiplas tentativas.", cacheKey);
            throw new IOException($"Falha ao baixar bloco {chunkIndex} da parte {part.PartIndex} do Telegram.");
        }

        InsertIntoCache(cacheKey, chunkData);
        return chunkData;
    }

    private void InsertIntoCache(string cacheKey, byte[] chunkData)
    {
        _chunkCache[cacheKey] = chunkData;
        _lruOrder.Remove(cacheKey);
        _lruOrder.AddFirst(cacheKey);

        while (_chunkCache.Count > MaxCachedChunks && _lruOrder.Last != null)
        {
            var oldestKey = _lruOrder.Last.Value;
            _lruOrder.RemoveLast();
            _chunkCache.Remove(oldestKey);
        }
    }

    private async Task<byte[]> FetchChunkDataAsync(NebulaStreamPart part, int chunkIndex, CancellationToken cancellationToken)
    {
        var cacheKey = $"{part.PartIndex}:{chunkIndex}";
        var chunkOffsetInPart = (long)chunkIndex * ChunkSize;
        var chunkLimit = (int)Math.Min(ChunkSize, part.Size - chunkOffsetInPart);
        if (chunkLimit <= 0)
        {
            return Array.Empty<byte>();
        }

        byte[]? chunkData = null;

        // 1. Tenta leitura direta se arquivo local existir em disco
        if (!string.IsNullOrWhiteSpace(part.LocalPath) && File.Exists(part.LocalPath))
        {
            try
            {
                var diskBuf = new byte[chunkLimit];
                await using var fs = new FileStream(part.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
                fs.Seek(chunkOffsetInPart, SeekOrigin.Begin);
                var read = await fs.ReadAsync(diskBuf.AsMemory(0, chunkLimit), cancellationToken).ConfigureAwait(false);
                if (read < chunkLimit)
                {
                    Array.Resize(ref diskBuf, read);
                }

                chunkData = diskBuf;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NEBULA-STREAM] Falha ao ler chunk local {Key}, tentando Telegram...", cacheKey);
            }
        }

        // 2. Download do chunk sob demanda via Telegram MTProto / Bot API Range com até 5 tentativas e backoff progressivo
        if ((chunkData == null || chunkData.Length == 0) && _telegramPool != null)
        {
            const int maxAttempts = 5;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _logger.LogDebug("[NEBULA-STREAM] Baixando chunk {Key} (offset: {Offset}, tamanho: {Size}, tentativa {Attempt}/{Max})...", cacheKey, chunkOffsetInPart, chunkLimit, attempt, maxAttempts);
                    chunkData = await _telegramPool.DownloadChunkAsync(
                        part.FileId,
                        part.BotIndex,
                        part.ChatId,
                        part.MessageId,
                        chunkOffsetInPart,
                        chunkLimit,
                        cancellationToken).ConfigureAwait(false);

                    if (chunkData != null && chunkData.Length > 0)
                    {
                        break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NEBULA-STREAM] Exceção ao tentar baixar chunk {Key} (tentativa {Attempt}/{Max}).", cacheKey, attempt, maxAttempts);
                }

                if (attempt < maxAttempts)
                {
                    var delayMs = attempt switch
                    {
                        1 => 200,
                        2 => 500,
                        3 => 1000,
                        _ => 2000
                    };
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return chunkData ?? Array.Empty<byte>();
    }

    private int GetChunkLength(NebulaStreamPart part, int chunkIndex)
    {
        var offset = (long)chunkIndex * ChunkSize;
        return (int)Math.Min(ChunkSize, Math.Max(0, part.Size - offset));
    }

    private async Task PrefetchWholeMediaAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var part in _parts)
            {
                var chunks = (int)Math.Ceiling((double)part.Size / ChunkSize);
                for (var chunkIndex = 0; chunkIndex < chunks; chunkIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _ = await _playbackCache!.GetOrFetchChunkAsync(
                        _mediaKey,
                        part.PartIndex,
                        chunkIndex,
                        GetChunkLength(part, chunkIndex),
                        token => FetchChunkDataAsync(part, chunkIndex, token),
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-STREAM-PREFETCH] Pré-cache da mídia {MediaKey} interrompido.", _mediaKey);
        }
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            _disposed = true;

            if (disposing)
            {
                try
                {
                _streamCts.Cancel();
                _streamCts.Dispose();
                _playbackLease?.Dispose();
                }
                catch
                {
                }

                _inflightPrefetches.Clear();
                _chunkCache.Clear();
                _lruOrder.Clear();
                try
                {
                    _lock.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        base.Dispose(disposing);
    }
}
