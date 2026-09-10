using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private const int MaxCachedChunks = 16;     // 16 MB máximo de buffer LRU por stream ativo

    private readonly NebulaTelegramPool? _telegramPool;
    private readonly IReadOnlyList<NebulaStreamPart> _parts;
    private readonly long _totalLength;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly Dictionary<string, byte[]> _chunkCache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lruOrder = new();

    private long _position;
    private bool _disposed;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaChunkedStream"/>.
    /// </summary>
    public NebulaChunkedStream(
        NebulaTelegramPool? telegramPool,
        IEnumerable<NebulaStreamPart> parts,
        long? totalLength,
        ILogger logger)
    {
        _telegramPool = telegramPool;
        _parts = parts.OrderBy(p => p.PartIndex).ToList();
        _logger = logger;

        if (_parts.Count > 0)
        {
            var calculatedLength = _parts[^1].FileOffset + _parts[^1].Size;
            _totalLength = totalLength.HasValue && totalLength.Value > 0 ? totalLength.Value : calculatedLength;
        }
        else
        {
            _totalLength = totalLength ?? 0;
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
        return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
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

    private async Task<byte[]> GetChunkAsync(NebulaStreamPart part, int chunkIndex, CancellationToken cancellationToken)
    {
        var cacheKey = $"{part.PartIndex}:{chunkIndex}";
        if (_chunkCache.TryGetValue(cacheKey, out var cachedData))
        {
            _lruOrder.Remove(cacheKey);
            _lruOrder.AddFirst(cacheKey);
            return cachedData;
        }

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

        // 2. Download do chunk sob demanda via Telegram MTProto / Bot API Range
        if ((chunkData == null || chunkData.Length == 0) && _telegramPool != null)
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _logger.LogDebug("[NEBULA-STREAM] Baixando chunk {Key} (offset: {Offset}, tamanho: {Size}, tentativa {Attempt}/3)...", cacheKey, chunkOffsetInPart, chunkLimit, attempt);
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
                    _logger.LogWarning(ex, "[NEBULA-STREAM] Exceção ao tentar baixar chunk {Key} (tentativa {Attempt}/3).", cacheKey, attempt);
                }

                if (attempt < 3)
                {
                    await Task.Delay(attempt * 250, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (chunkData == null || chunkData.Length == 0)
        {
            _logger.LogError("[NEBULA-STREAM] Falha ao baixar chunk {Key} do Telegram.", cacheKey);
            throw new IOException($"Falha ao baixar bloco {chunkIndex} da parte {part.PartIndex} do Telegram.");
        }

        // 3. Insere no cache LRU delimitado
        _chunkCache[cacheKey] = chunkData;
        _lruOrder.AddFirst(cacheKey);

        while (_chunkCache.Count > MaxCachedChunks && _lruOrder.Last != null)
        {
            var oldestKey = _lruOrder.Last.Value;
            _lruOrder.RemoveLast();
            _chunkCache.Remove(oldestKey);
        }

        return chunkData;
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
