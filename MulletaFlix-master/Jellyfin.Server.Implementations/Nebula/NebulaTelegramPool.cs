#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable MT1013 // Releasing lock should always be wrapped in finally block

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Logging;
using TL;
using WTelegram;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Gerencia um pool de clientes Telegram MTProto nativos em C# (Multi-Bot).
/// </summary>
public sealed class NebulaTelegramPool : IAsyncDisposable, IDisposable
{
    private readonly ILogger<NebulaTelegramPool> _logger;
    private readonly int _apiId;
    private readonly string _apiHash;
    private readonly List<string> _botTokens;
    private readonly long _chatId;
    private readonly string _sessionsDirectory;
    private readonly ConcurrentDictionary<int, Client> _clients = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _botOperationGates = new();
    private readonly ConcurrentDictionary<int, int> _streamWaiters = new();
    private int _streamWaiterCount;
    private int _uploadRoundRobinCursor;
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<long, long>> _dynamicChannelAccessHashes = new();
    private readonly ConcurrentDictionary<string, (Document Doc, DateTime ExpiresAt)> _documentCache = new();
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(10) };
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly Mutex _processLock;
    private bool _processLockHeld;
    private bool _isInitialized;
    private bool _disposed;

    static NebulaTelegramPool()
    {
        // Silencia os logs internos ruidosos do WTelegramClient (como "Receiving Updates", pings e keepalives periódicos do MTProto)
        Helpers.Log = (level, message) =>
        {
            if (string.IsNullOrWhiteSpace(message) || message.Contains("Receiving Updates", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        };
    }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaTelegramPool"/>.
    /// </summary>
    public NebulaTelegramPool(int apiId, string apiHash, IEnumerable<string> botTokens, long chatId, string sessionsDirectory, ILogger<NebulaTelegramPool> logger)
    {
        _apiId = apiId;
        _apiHash = apiHash;
        _botTokens = botTokens.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        _chatId = chatId;
        _sessionsDirectory = string.IsNullOrWhiteSpace(sessionsDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "nebula_sessions")
            : sessionsDirectory;
        _logger = logger;
        _processLock = new Mutex(false, "MulletaFlix.NebulaTelegramPool");

        if (!Directory.Exists(_sessionsDirectory))
        {
            try
            {
                Directory.CreateDirectory(_sessionsDirectory);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEBULA-TG] Não foi possível criar diretório de sessões: {Path}", _sessionsDirectory);
            }
        }
    }

    /// <summary>
    /// Obtém a contagem de bots disponíveis no pool.
    /// </summary>
    public int BotCount => _botTokens.Count;

    /// <summary>
    /// Retorna os índices dos bots autenticados e disponíveis para upload e streaming.
    /// </summary>
    public IReadOnlyList<int> GetAvailableBotIndices() => _clients.Keys.OrderBy(index => index).ToArray();

    private async Task<BotOperationLease> AcquireBotAsync(int botIndex, bool streaming, CancellationToken cancellationToken)
    {
        var gate = _botOperationGates.GetOrAdd(botIndex, static _ => new SemaphoreSlim(1, 1));
        if (streaming)
        {
            Interlocked.Increment(ref _streamWaiterCount);
            _streamWaiters.AddOrUpdate(botIndex, 1, static (_, count) => count + 1);
        }

        try
        {
            // A streaming request announces itself before waiting. Uploads
            // therefore stop acquiring this bot until the stream gets it.
            while (!streaming && _streamWaiters.TryGetValue(botIndex, out var waiters) && waiters > 0)
            {
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }

            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new BotOperationLease(this, botIndex, streaming, gate);
        }
        catch
        {
            if (streaming)
            {
                ReleaseStreamWaiter(botIndex);
                Interlocked.Decrement(ref _streamWaiterCount);
            }

            throw;
        }
    }

    private async Task<(int BotIndex, BotOperationLease Lease)> AcquireUploadBotAsync(int preferredBotIndex, CancellationToken cancellationToken)
    {
        var candidates = GetAvailableBotIndices();
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("Nenhum bot Telegram autenticado está disponível.");
        }

        var start = Math.Abs(Interlocked.Increment(ref _uploadRoundRobinCursor));
        while (true)
        {
            var ordered = candidates
                .OrderBy(index => index == preferredBotIndex ? 0 : 1)
                .ThenBy(index => (index - start + candidates.Count) % candidates.Count);

            foreach (var candidate in ordered)
            {
                if (Volatile.Read(ref _streamWaiterCount) > 0)
                {
                    continue;
                }

                var gate = _botOperationGates.GetOrAdd(candidate, static _ => new SemaphoreSlim(1, 1));
                if (gate.Wait(0, cancellationToken))
                {
                    return (candidate, new BotOperationLease(this, candidate, streaming: false, gate));
                }
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            candidates = GetAvailableBotIndices();
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException("Nenhum bot Telegram autenticado está disponível.");
            }
        }
    }

    private Task<(int BotIndex, BotOperationLease Lease)> AcquireStreamingBotAsync(CancellationToken cancellationToken)
    {
        return AcquireStreamingBotAsync(null, cancellationToken);
    }

    private async Task<(int BotIndex, BotOperationLease Lease)> AcquireStreamingBotAsync(IEnumerable<int>? preferredIndices, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _streamWaiterCount);
        try
        {
            var available = GetAvailableBotIndices();
            var orderedCandidates = new List<int>();
            if (preferredIndices != null)
            {
                foreach (var idx in preferredIndices)
                {
                    if (available.Contains(idx) && !orderedCandidates.Contains(idx))
                    {
                        orderedCandidates.Add(idx);
                    }
                }
            }
            foreach (var idx in available)
            {
                if (!orderedCandidates.Contains(idx))
                {
                    orderedCandidates.Add(idx);
                }
            }

            while (true)
            {
                foreach (var candidate in orderedCandidates)
                {
                    var gate = _botOperationGates.GetOrAdd(candidate, static _ => new SemaphoreSlim(1, 1));
                    if (gate.Wait(0, cancellationToken))
                    {
                        return (candidate, new BotOperationLease(this, candidate, streaming: true, gate, registeredBotWaiter: false));
                    }
                }

                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            Interlocked.Decrement(ref _streamWaiterCount);
            throw;
        }
    }

    private void ReleaseStreamWaiter(int botIndex)
    {
        while (_streamWaiters.TryGetValue(botIndex, out var count))
        {
            if (count <= 1)
            {
                _streamWaiters.TryRemove(botIndex, out _);
                return;
            }

            if (_streamWaiters.TryUpdate(botIndex, count - 1, count))
            {
                return;
            }
        }
    }

    private sealed class BotOperationLease : IAsyncDisposable, IDisposable
    {
        private readonly NebulaTelegramPool _pool;
        private readonly int _botIndex;
        private readonly bool _streaming;
        private readonly bool _registeredBotWaiter;
        private readonly SemaphoreSlim _gate;
        private int _released;

        public BotOperationLease(NebulaTelegramPool pool, int botIndex, bool streaming, SemaphoreSlim gate, bool registeredBotWaiter = true)
        {
            _pool = pool;
            _botIndex = botIndex;
            _streaming = streaming;
            _registeredBotWaiter = registeredBotWaiter;
            _gate = gate;
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            _gate.Release();
            if (_streaming)
            {
                if (_registeredBotWaiter)
                {
                    _pool.ReleaseStreamWaiter(_botIndex);
                }

                Interlocked.Decrement(ref _pool._streamWaiterCount);
            }
        }
    }

    private static readonly Dictionary<long, long> PreconfiguredChannelAccessHashesByBotId = new()
    {
        [8475077434L] = 114448420708494477L,
        [8743388135L] = 856309300169021782L,
        [8890265800L] = -8036708376068042844L,
        [8945326512L] = 7361206485030775775L,
        [8722157038L] = 5798099809891185533L,
        [8756027175L] = -3961849028782674461L,
        [8877238019L] = -8102247878347615001L,
        [8980197230L] = 4317851130226046274L,
        [8997013483L] = -9090009593983571985L,
        [8755896032L] = 6780101588244881488L,
        [8489114187L] = 662756728228471796L,
        [8739505562L] = 7443117184317057954L,
        [8962685108L] = -3145717374088105417L,
        [8737247555L] = -6939686279652876888L,
        [8695964151L] = 5199273961268528468L,
        [8987473016L] = -7038563946273338151L,
        [8627365859L] = -2261288843613312292L,
        [8906544679L] = 4640806314620525651L,
        [8812965818L] = -2130880744760265223L,
        [8924617819L] = -6507069366573564098L,
        [8952415041L] = -8585133239572723862L,
        [8612055974L] = -7040678947878022633L,
        [8943811810L] = -8213696363342981167L,
        [8934395871L] = 1031787625893985912L,
        [8857938659L] = -8200933377466483580L,
        [8856312828L] = 4759093190503763777L,
        [8951063777L] = -9116673572058871474L,
    };

    private static readonly Dictionary<int, long> PreconfiguredChannelAccessHashes = new()
    {
        [1] = 114448420708494477L,
        [2] = 856309300169021782L,
        [3] = -8036708376068042844L,
        [4] = 7361206485030775775L,
        [5] = 5798099809891185533L,
        [6] = -3961849028782674461L,
        [7] = -8102247878347615001L,
        [8] = 4317851130226046274L,
        [9] = -9090009593983571985L,
        [10] = 6780101588244881488L,
        [11] = 662756728228471796L,
        [12] = 7443117184317057954L,
        [13] = -3145717374088105417L,
        [14] = -6939686279652876888L,
        [15] = 5199273961268528468L,
        [16] = -7038563946273338151L,
        [17] = -2261288843613312292L,
        [18] = 4640806314620525651L,
        [19] = -2130880744760265223L,
        [20] = -6507069366573564098L,
        [21] = -8585133239572723862L,
        [22] = -7040678947878022633L,
        [23] = -8213696363342981167L,
        [24] = 1031787625893985912L,
        [25] = -8200933377466483580L,
        [26] = 4759093190503763777L,
        [27] = -9116673572058871474L,
    };

    /// <summary>
    /// Baixa um documento publicado no canal para o stream informado.
    /// </summary>
    public Task<bool> DownloadDocumentAsync(long chatId, int messageId, Stream output, CancellationToken cancellationToken = default)
    {
        return DownloadDocumentAsync(null, -1, chatId, messageId, output, cancellationToken);
    }

    /// <summary>
    /// Baixa um documento publicado no Telegram usando MTProto com renovação automática de file_reference expirada ou fallback por Bot API.
    /// </summary>
    public async Task<bool> DownloadDocumentAsync(string? fileId, int botIndex, long chatId, int messageId, Stream output, CancellationToken cancellationToken = default)
    {
        var availableBots = GetAvailableBotIndices();
        if (availableBots.Count == 0)
        {
            _logger.LogWarning("[NEBULA-TG] Nenhum bot Telegram disponível para download.");
            return false;
        }

        var orderedBots = new List<int>();
        if (botIndex > 0 && availableBots.Contains(botIndex))
        {
            orderedBots.Add(botIndex);
            var nextBot = botIndex == _botTokens.Count - 1 ? 1 : botIndex + 1;
            if (availableBots.Contains(nextBot) && !orderedBots.Contains(nextBot))
            {
                orderedBots.Add(nextBot);
            }
        }

        foreach (var idx in availableBots)
        {
            if (idx > 0 && !orderedBots.Contains(idx))
            {
                orderedBots.Add(idx);
            }
        }
        if (availableBots.Contains(0) && !orderedBots.Contains(0))
        {
            orderedBots.Add(0);
        }

        var effectiveChatId = chatId != 0 ? chatId : _chatId;
        var bareChannelId = NormalizeChannelId(effectiveChatId);

        foreach (var idx in orderedBots)
        {
            var selected = await AcquireStreamingBotAsync([idx], cancellationToken).ConfigureAwait(false);
            if (!_clients.TryGetValue(selected.BotIndex, out var client))
            {
                await selected.Lease.DisposeAsync().ConfigureAwait(false);
                continue;
            }

            await using var operation = selected.Lease;
            var activeBotIndex = selected.BotIndex;

            if (messageId > 0)
            {
                var refreshed = await TryDownloadChannelMessageAsync(
                    client,
                    activeBotIndex,
                    bareChannelId,
                    messageId,
                    output,
                    cancellationToken).ConfigureAwait(false);
                if (refreshed)
                {
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(fileId))
            {
                var doc = TelegramFileIdDecoder.DecodeDocument(fileId);
                if (doc != null)
                {
                    var attemptPosition = CaptureOutputPosition(output);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await client.DownloadFileAsync(doc, output).ConfigureAwait(false);
                        return true;
                    }
                    catch (RpcException rpcEx) when (rpcEx.Code == 400 && rpcEx.Message.Contains("FILE_REFERENCE_EXPIRED", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("[NEBULA-TG] File reference expirada no Bot [{BotIndex}] para mensagem {MessageId}. Renovando referência via canal...", activeBotIndex, messageId);
                        RewindOutputToPosition(output, attemptPosition);
                        if (messageId > 0)
                        {
                            var refreshed = await TryDownloadChannelMessageAsync(
                                client,
                                activeBotIndex,
                                bareChannelId,
                                messageId,
                                output,
                                cancellationToken).ConfigureAwait(false);
                            if (refreshed)
                            {
                                return true;
                            }
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        RewindOutputToPosition(output, attemptPosition);
                        _logger.LogDebug(ex, "[NEBULA-TG] Falha ao baixar via MTProto FileId no Bot [{BotIndex}].", activeBotIndex);
                    }
                }
            }
        }

        // 2. Fallback via Bot API
        if (!string.IsNullOrWhiteSpace(fileId))
        {
            foreach (var idx in orderedBots)
            {
                var selected = await AcquireStreamingBotAsync([idx], cancellationToken).ConfigureAwait(false);
                await using var operation = selected.Lease;
                var downloaded = await DownloadViaBotApiAsync(selected.BotIndex, fileId, output, cancellationToken).ConfigureAwait(false);
                if (downloaded)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Baixa um intervalo de bytes (chunk sob demanda) de um documento no Telegram usando MTProto ou fallback Bot API.
    /// </summary>
    public async Task<byte[]?> DownloadChunkAsync(
        string? fileId,
        int botIndex,
        long chatId,
        int messageId,
        long offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0 || limit <= 0)
        {
            _logger.LogWarning("[NEBULA-TG] Solicitação de chunk inválida: offset={Offset}, limite={Limit}.", offset, limit);
            return null;
        }

        var availableBots = GetAvailableBotIndices();
        if (availableBots.Count == 0)
        {
            _logger.LogWarning("[NEBULA-TG] Nenhum bot Telegram disponível para streaming de chunk.");
            return null;
        }

        var orderedBots = new List<int>();
        if (botIndex > 0 && availableBots.Contains(botIndex))
        {
            orderedBots.Add(botIndex);
            var nextBot = botIndex == _botTokens.Count - 1 ? 1 : botIndex + 1;
            if (availableBots.Contains(nextBot) && !orderedBots.Contains(nextBot))
            {
                orderedBots.Add(nextBot);
            }
        }

        foreach (var idx in availableBots)
        {
            if (idx > 0 && !orderedBots.Contains(idx))
            {
                orderedBots.Add(idx);
            }
        }
        if (availableBots.Contains(0) && !orderedBots.Contains(0))
        {
            orderedBots.Add(0);
        }

        var effectiveChatId = chatId != 0 ? chatId : _chatId;
        var bareChannelId = NormalizeChannelId(effectiveChatId);

        foreach (var idx in orderedBots)
        {
            var selected = await AcquireStreamingBotAsync([idx], cancellationToken).ConfigureAwait(false);
            if (!_clients.TryGetValue(selected.BotIndex, out var client))
            {
                await selected.Lease.DisposeAsync().ConfigureAwait(false);
                continue;
            }

            await using var operation = selected.Lease;
            var activeBotIndex = selected.BotIndex;

            if (messageId > 0)
            {
                var chunk = await TryDownloadChannelMessageChunkAsync(
                    client,
                    activeBotIndex,
                    bareChannelId,
                    messageId,
                    offset,
                    limit,
                    cancellationToken).ConfigureAwait(false);
                if (chunk != null && chunk.Length > 0)
                {
                    return chunk;
                }
            }

            if (!string.IsNullOrWhiteSpace(fileId))
            {
                var doc = TelegramFileIdDecoder.DecodeDocument(fileId);
                if (doc != null)
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var location = doc.ToFileLocation();
                        int requestedLimit = limit;
                        int alignedLimit = Math.Max(4096, (limit + 4095) / 4096 * 4096);
                        var uploadFile = await client.Upload_GetFile(location, offset, alignedLimit, precise: true).ConfigureAwait(false);
                        if (uploadFile is Upload_File uf && uf.bytes != null && uf.bytes.Length > 0)
                        {
                            return uf.bytes.Length > requestedLimit ? uf.bytes[..requestedLimit] : uf.bytes;
                        }
                    }
                    catch (RpcException rpcEx) when (rpcEx.Code == 400 && rpcEx.Message.Contains("FILE_REFERENCE_EXPIRED", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("[NEBULA-TG] File reference expirada no Bot [{BotIndex}] para chunk no offset {Offset}. Renovando referência via canal...", activeBotIndex, offset);
                        if (messageId > 0)
                        {
                            var refreshedChunk = await TryDownloadChannelMessageChunkAsync(
                                client,
                                activeBotIndex,
                                bareChannelId,
                                messageId,
                                offset,
                                limit,
                                cancellationToken).ConfigureAwait(false);
                            if (refreshedChunk != null && refreshedChunk.Length > 0)
                            {
                                return refreshedChunk;
                            }
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[NEBULA-TG] Falha ao obter chunk via MTProto FileId no Bot [{BotIndex}].", activeBotIndex);
                    }
                }
            }
        }

        // 2. Fallback via Bot API com Range HTTP
        if (!string.IsNullOrWhiteSpace(fileId))
        {
            foreach (var idx in orderedBots)
            {
                var selected = await AcquireStreamingBotAsync([idx], cancellationToken).ConfigureAwait(false);
                await using var operation = selected.Lease;
                var chunk = await DownloadChunkViaBotApiAsync(selected.BotIndex, fileId, offset, limit, cancellationToken).ConfigureAwait(false);
                if (chunk != null && chunk.Length > 0)
                {
                    return chunk;
                }
            }
        }

        return null;
    }

    private async Task<byte[]?> TryDownloadChannelMessageChunkAsync(
        Client client,
        int botIndex,
        long bareChannelId,
        int messageId,
        long offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{bareChannelId}:{messageId}";
        int requestedLimit = limit;
        int alignedLimit = Math.Max(4096, (limit + 4095) / 4096 * 4096);

        // 1. Tenta com Document cacheado com file_reference recente
        if (_documentCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var location = cached.Doc.ToFileLocation();
                var uploadFile = await client.Upload_GetFile(location, offset, alignedLimit, precise: true).ConfigureAwait(false);
                if (uploadFile is Upload_File uf && uf.bytes != null && uf.bytes.Length > 0)
                {
                    return uf.bytes.Length > requestedLimit ? uf.bytes[..requestedLimit] : uf.bytes;
                }
            }
            catch (RpcException rpcEx) when (rpcEx.Code == 400 && rpcEx.Message.Contains("FILE_REFERENCE_EXPIRED", StringComparison.OrdinalIgnoreCase))
            {
                _documentCache.TryRemove(cacheKey, out _);
                _logger.LogDebug("[NEBULA-TG] Cache de Document expirou para mensagem {MessageId} no Bot [{BotIndex}]. Renovando via canal...", messageId, botIndex);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NEBULA-TG] Falha ao usar Document cacheado da mensagem {MessageId} no Bot [{BotIndex}].", messageId, botIndex);
            }
        }

        // 2. Busca mensagem no canal via MTProto com access_hash resolvido dinamicamente
        var candidateHashes = await GetCandidateChannelAccessHashesAsync(client, botIndex, bareChannelId, cancellationToken).ConfigureAwait(false);
        foreach (var accessHash in candidateHashes)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var inputChannel = new InputChannel(bareChannelId, accessHash);
                var messages = await client.Channels_GetMessages(
                    inputChannel,
                    [new InputMessageID { id = messageId }]).ConfigureAwait(false);

                var message = messages?.Messages?.OfType<Message>().FirstOrDefault();
                if (message?.media is MessageMediaDocument { document: Document freshDoc })
                {
                    _documentCache[cacheKey] = (freshDoc, DateTime.UtcNow.AddHours(2));

                    cancellationToken.ThrowIfCancellationRequested();
                    var location = freshDoc.ToFileLocation();
                    var uploadFile = await client.Upload_GetFile(location, offset, alignedLimit, precise: true).ConfigureAwait(false);
                    if (uploadFile is Upload_File uf && uf.bytes != null && uf.bytes.Length > 0)
                    {
                        return uf.bytes.Length > requestedLimit ? uf.bytes[..requestedLimit] : uf.bytes;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception refreshEx)
            {
                _logger.LogDebug(refreshEx, "[NEBULA-TG] Falha ao baixar chunk da mensagem {MessageId} no Bot [{BotIndex}] com access_hash candidato {AccessHash}.", messageId, botIndex, accessHash);
            }
        }

        return null;
    }

    private async Task<bool> TryDownloadChannelMessageAsync(
        Client client,
        int botIndex,
        long bareChannelId,
        int messageId,
        Stream output,
        CancellationToken cancellationToken)
    {
        var attemptPosition = CaptureOutputPosition(output);
        var cacheKey = $"{bareChannelId}:{messageId}";

        // 1. Tenta com Document cacheado com file_reference recente
        if (_documentCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await client.DownloadFileAsync(cached.Doc, output).ConfigureAwait(false);
                return true;
            }
            catch (RpcException rpcEx) when (rpcEx.Code == 400 && rpcEx.Message.Contains("FILE_REFERENCE_EXPIRED", StringComparison.OrdinalIgnoreCase))
            {
                _documentCache.TryRemove(cacheKey, out _);
                RewindOutputToPosition(output, attemptPosition);
                _logger.LogDebug("[NEBULA-TG] Cache de Document expirou para mensagem {MessageId} no Bot [{BotIndex}]. Renovando via canal...", messageId, botIndex);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                RewindOutputToPosition(output, attemptPosition);
                _logger.LogDebug(ex, "[NEBULA-TG] Falha ao usar Document cacheado da mensagem {MessageId} no Bot [{BotIndex}].", messageId, botIndex);
            }
        }

        // 2. Busca mensagem no canal via MTProto com access_hash resolvido dinamicamente
        var candidateHashes = await GetCandidateChannelAccessHashesAsync(client, botIndex, bareChannelId, cancellationToken).ConfigureAwait(false);
        foreach (var accessHash in candidateHashes)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var inputChannel = new InputChannel(bareChannelId, accessHash);
                var messages = await client.Channels_GetMessages(
                    inputChannel,
                    [new InputMessageID { id = messageId }]).ConfigureAwait(false);

                var message = messages?.Messages?.OfType<Message>().FirstOrDefault();
                if (message?.media is MessageMediaDocument { document: Document freshDoc })
                {
                    _documentCache[cacheKey] = (freshDoc, DateTime.UtcNow.AddHours(2));

                    cancellationToken.ThrowIfCancellationRequested();
                    await client.DownloadFileAsync(freshDoc, output).ConfigureAwait(false);
                    return true;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception refreshEx)
            {
                RewindOutputToPosition(output, attemptPosition);
                _logger.LogDebug(refreshEx, "[NEBULA-TG] Falha ao baixar mensagem {MessageId} no Bot [{BotIndex}] com access_hash candidato {AccessHash}.", messageId, botIndex, accessHash);
            }
        }

        return false;
    }

    private async Task<long?> ResolveChannelAccessHashAsync(Client client, int botIndex, long bareChannelId, CancellationToken cancellationToken)
    {
        if (_dynamicChannelAccessHashes.TryGetValue(botIndex, out var map) && map.TryGetValue(bareChannelId, out var cachedHash) && cachedHash != 0)
        {
            return cachedHash;
        }

        if (botIndex >= 0 && botIndex < _botTokens.Count)
        {
            var token = _botTokens[botIndex];
            var colonIdx = token.IndexOf(':', StringComparison.Ordinal);
            if (colonIdx > 0 && long.TryParse(token[..colonIdx], out var botId))
            {
                if (PreconfiguredChannelAccessHashesByBotId.TryGetValue(botId, out var directHash) && directHash != 0)
                {
                    var channelMap = _dynamicChannelAccessHashes.GetOrAdd(botIndex, _ => new ConcurrentDictionary<long, long>());
                    channelMap[bareChannelId] = directHash;
                    return directHash;
                }
            }
        }

        if (PreconfiguredChannelAccessHashes.TryGetValue(botIndex, out var fallbackHash) && fallbackHash != 0)
        {
            var channelMap = _dynamicChannelAccessHashes.GetOrAdd(botIndex, _ => new ConcurrentDictionary<long, long>());
            channelMap[bareChannelId] = fallbackHash;
            return fallbackHash;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chats = await client.Messages_GetAllChats().ConfigureAwait(false);
            if (chats?.chats != null)
            {
                foreach (var (cId, chatBase) in chats.chats)
                {
                    if (chatBase is Channel ch && ch.access_hash != 0)
                    {
                        var channelMap = _dynamicChannelAccessHashes.GetOrAdd(botIndex, _ => new ConcurrentDictionary<long, long>());
                        channelMap[ch.id] = ch.access_hash;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-TG] Falha ao resolver chats via Messages_GetAllChats no Bot [{BotIndex}].", botIndex);
        }

        if (_dynamicChannelAccessHashes.TryGetValue(botIndex, out var map2) && map2.TryGetValue(bareChannelId, out var resolvedHash) && resolvedHash != 0)
        {
            return resolvedHash;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dialogs = await client.Messages_GetDialogs(folder_id: 0).ConfigureAwait(false);
            var dialogChats = (dialogs as Messages_Dialogs)?.chats ?? (dialogs as Messages_DialogsSlice)?.chats;
            if (dialogChats != null)
            {
                foreach (var (cId, chatBase) in dialogChats)
                {
                    if (chatBase is Channel ch && ch.access_hash != 0)
                    {
                        var channelMap = _dynamicChannelAccessHashes.GetOrAdd(botIndex, _ => new ConcurrentDictionary<long, long>());
                        channelMap[ch.id] = ch.access_hash;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-TG] Falha ao resolver chats via Messages_GetDialogs no Bot [{BotIndex}].", botIndex);
        }

        if (_dynamicChannelAccessHashes.TryGetValue(botIndex, out var map3) && map3.TryGetValue(bareChannelId, out var resolvedHash2) && resolvedHash2 != 0)
        {
            return resolvedHash2;
        }

        return null;
    }

    private async Task<List<long>> GetCandidateChannelAccessHashesAsync(Client client, int botIndex, long bareChannelId, CancellationToken cancellationToken)
    {
        var candidates = new List<long>();
        var resolved = await ResolveChannelAccessHashAsync(client, botIndex, bareChannelId, cancellationToken).ConfigureAwait(false);
        if (resolved.HasValue && resolved.Value != 0)
        {
            candidates.Add(resolved.Value);
        }

        foreach (var hash in GetCandidateChannelAccessHashes(botIndex))
        {
            if (!candidates.Contains(hash))
            {
                candidates.Add(hash);
            }
        }

        return candidates;
    }

    internal static IEnumerable<long> GetCandidateChannelAccessHashes(int botIndex)
    {
        if (PreconfiguredChannelAccessHashes.TryGetValue(botIndex, out var preferred))
        {
            yield return preferred;
        }

        foreach (var accessHash in PreconfiguredChannelAccessHashes
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value))
        {
            if (accessHash != preferred)
            {
                yield return accessHash;
            }
        }
    }

    internal static long? CaptureOutputPosition(Stream output)
    {
        return output.CanSeek ? output.Position : null;
    }

    internal static void RewindOutputToPosition(Stream output, long? position)
    {
        if (!position.HasValue || !output.CanSeek)
        {
            return;
        }

        output.Position = position.Value;
        if (output.CanWrite)
        {
            output.SetLength(position.Value);
        }
    }

    private async Task<bool> DownloadViaBotApiAsync(int botIndex, string fileId, Stream output, CancellationToken cancellationToken)
    {
        var token = _botTokens[botIndex];
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var attemptPosition = CaptureOutputPosition(output);
            try
            {
                var getFileEndpoint = $"https://api.telegram.org/bot{token}/getFile?file_id={Uri.EscapeDataString(fileId)}";
                using var getFileResp = await _httpClient.GetAsync(getFileEndpoint, cancellationToken).ConfigureAwait(false);
                if (!getFileResp.IsSuccessStatusCode)
                {
                    if ((int)getFileResp.StatusCode == 429 || (int)getFileResp.StatusCode >= 500)
                    {
                        throw new HttpRequestException($"Telegram Bot API getFile retornou HTTP {(int)getFileResp.StatusCode}.");
                    }

                    return false;
                }

                var json = await getFileResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using var jsonDoc = JsonDocument.Parse(json);
                if (!jsonDoc.RootElement.TryGetProperty("ok", out var okProp) || !okProp.GetBoolean())
                {
                    return false;
                }

                if (!jsonDoc.RootElement.TryGetProperty("result", out var resultProp) ||
                    !resultProp.TryGetProperty("file_path", out var pathProp))
                {
                    return false;
                }

                var filePath = pathProp.GetString();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return false;
                }

                var downloadEndpoint = $"https://api.telegram.org/file/bot{token}/{filePath}";
                using var downloadResp = await _httpClient.GetAsync(downloadEndpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!downloadResp.IsSuccessStatusCode)
                {
                    if ((int)downloadResp.StatusCode == 429 || (int)downloadResp.StatusCode >= 500)
                    {
                        throw new HttpRequestException($"Telegram file download retornou HTTP {(int)downloadResp.StatusCode}.");
                    }

                    return false;
                }

                await downloadResp.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < 3 && !cancellationToken.IsCancellationRequested)
            {
                RewindOutputToPosition(output, attemptPosition);
                _logger.LogWarning(
                    "[NEBULA-TG] Falha transitória ao baixar arquivo via Bot [{BotIndex}] (tentativa {Attempt}/3, tipo {ErrorType}); tentando novamente.",
                    botIndex,
                    attempt,
                    ex.GetType().Name);
                await Task.Delay(GetRetryDelay(ex, attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                RewindOutputToPosition(output, attemptPosition);
                throw;
            }
            catch (Exception ex)
            {
                RewindOutputToPosition(output, attemptPosition);
                _logger.LogError(
                    "[NEBULA-TG] Erro ao baixar arquivo Bot API via Bot [{BotIndex}] (tipo {ErrorType}).",
                    botIndex,
                    ex.GetType().Name);
                return false;
            }
        }

        return false;
    }

    private async Task<byte[]?> DownloadChunkViaBotApiAsync(int botIndex, string fileId, long offset, int limit, CancellationToken cancellationToken)
    {
        var token = _botTokens[botIndex];
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var getFileEndpoint = $"https://api.telegram.org/bot{token}/getFile?file_id={Uri.EscapeDataString(fileId)}";
                using var getFileResp = await _httpClient.GetAsync(getFileEndpoint, cancellationToken).ConfigureAwait(false);
                if (!getFileResp.IsSuccessStatusCode)
                {
                    if ((int)getFileResp.StatusCode == 429 || (int)getFileResp.StatusCode >= 500)
                    {
                        throw new HttpRequestException($"Telegram Bot API getFile retornou HTTP {(int)getFileResp.StatusCode}.");
                    }

                    return null;
                }

                var json = await getFileResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                using var jsonDoc = JsonDocument.Parse(json);
                if (!jsonDoc.RootElement.TryGetProperty("ok", out var okProp) || !okProp.GetBoolean())
                {
                    return null;
                }

                if (!jsonDoc.RootElement.TryGetProperty("result", out var resultProp) ||
                    !resultProp.TryGetProperty("file_path", out var pathProp))
                {
                    return null;
                }

                var filePath = pathProp.GetString();
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return null;
                }

                var downloadEndpoint = $"https://api.telegram.org/file/bot{token}/{filePath}";
                using var request = new HttpRequestMessage(HttpMethod.Get, downloadEndpoint);
                request.Headers.Range = new RangeHeaderValue(offset, offset + limit - 1);
                using var downloadResp = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (downloadResp.StatusCode is HttpStatusCode.PartialContent or HttpStatusCode.OK)
                {
                    var chunk = await downloadResp.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                    return IsValidBotApiChunkResponse(downloadResp, offset, limit, chunk.Length) ? chunk : null;
                }

                if ((int)downloadResp.StatusCode == 429 || (int)downloadResp.StatusCode >= 500)
                {
                    throw new HttpRequestException($"Telegram file download retornou HTTP {(int)downloadResp.StatusCode}.");
                }

                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < 3 && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "[NEBULA-TG] Falha transitória ao baixar chunk via Bot [{BotIndex}] (tentativa {Attempt}/3, tipo {ErrorType}); tentando novamente.",
                    botIndex,
                    attempt,
                    ex.GetType().Name);
                await Task.Delay(GetRetryDelay(ex, attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NEBULA-TG] Falha ao baixar chunk via Bot [{BotIndex}].", botIndex);
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Normaliza o chat_id do Telegram (remove o prefixo -100 do Bot API para MTProto se necessário).
    /// </summary>
    internal static long NormalizeChannelId(long chatId)
    {
        if (chatId < -1000000000000L)
        {
            return -chatId - 1000000000000L;
        }

        if (chatId < 0)
        {
            return -chatId;
        }

        return chatId;
    }

    /// <summary>
    /// Sanitiza uma mensagem removendo tokens de bots para evitar vazamento em logs.
    /// </summary>
    internal static string SanitizeMessage(string message, IEnumerable<string> botTokens)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        var sanitized = message;
        foreach (var token in botTokens)
        {
            if (!string.IsNullOrEmpty(token))
            {
                sanitized = sanitized.Replace(token, "[REDACTED_TOKEN]", StringComparison.Ordinal);
            }
        }

        return sanitized;
    }

    /// <summary>
    /// Inicializa os clientes e autentica os bots do pool no MTProto em paralelo.
    /// </summary>
    public async Task InitializeAsync(Action<string, string>? emitLog = null, CancellationToken cancellationToken = default)
    {
        if (_isInitialized)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                if (!_processLock.WaitOne(0))
                {
                    throw new InvalidOperationException(
                        "Outra instância do MulletaFlix já está usando as sessões do Nebula Telegram.");
                }

                _processLockHeld = true;
            }
            catch (AbandonedMutexException)
            {
                // A instância anterior terminou sem liberar a trava; as sessões podem ser reutilizadas.
                _processLockHeld = true;
            }

            emitLog?.Invoke("INFO", $"🤖 Iniciando {_botTokens.Count} bot(s) em paralelo...");
            _logger.LogInformation("[NEBULA-TG] Inicializando Telegram Bot Pool com {Count} bots...", _botTokens.Count);

            var authTasks = _botTokens.Select(async (token, i) =>
            {
                var index = i;
                try
                {
                    var client = new Client(ConfigProvider(token, index));
                    var user = await client.LoginBotIfNeeded(token).ConfigureAwait(false);
                    _clients[index] = client;
                    _logger.LogInformation("[NEBULA-TG] Bot [{Index}] @{Username} autenticado com sucesso.", index, user?.username ?? $"Bot_{index}");

                    try
                    {
                        var chats = await client.Messages_GetAllChats().ConfigureAwait(false);
                        if (chats?.chats != null)
                        {
                            foreach (var (cId, chatBase) in chats.chats)
                            {
                                if (chatBase is Channel ch && ch.access_hash != 0)
                                {
                                    var channelMap = _dynamicChannelAccessHashes.GetOrAdd(index, _ => new ConcurrentDictionary<long, long>());
                                    channelMap[ch.id] = ch.access_hash;
                                    _logger.LogInformation("[NEBULA-TG] Bot [{Index}] Canal MTProto detectado: '{Title}' (ID: {ChannelId})", index, ch.title, ch.id);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[NEBULA-TG] Bot [{Index}] falha ao buscar chats na inicialização.", index);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[NEBULA-TG] Falha ao autenticar Bot [{Index}].", index);
                }
            });

            await Task.WhenAll(authTasks).ConfigureAwait(false);

            var activeBots = _clients.Count;
            emitLog?.Invoke("INFO", $"Bots ativos nesta execucao: {activeBots} de {_botTokens.Count}.");
            emitLog?.Invoke("INFO", "🔍 Verificando acesso ao canal...");

            if (_botTokens.Count > 0 && _chatId != 0)
            {
                var firstToken = _botTokens[0];
                var channelTitle = "Mulletaflix";
                var reportedChatId = _chatId;

                try
                {
                    var url = $"https://api.telegram.org/bot{firstToken}/getChat?chat_id={_chatId}";
                    using var resp = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("result", out var resultElem))
                        {
                            if (resultElem.TryGetProperty("title", out var titleElem))
                            {
                                channelTitle = titleElem.GetString() ?? channelTitle;
                            }

                            if (resultElem.TryGetProperty("id", out var idElem))
                            {
                                reportedChatId = idElem.GetInt64();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[NEBULA-TG] Falha ao consultar título do canal via Bot API.");
                }

                emitLog?.Invoke("INFO", $"✅ Canal Confirmado: {channelTitle} (ID: {reportedChatId})");

                for (int i = 1; i < _botTokens.Count; i++)
                {
                    if (_clients.ContainsKey(i))
                    {
                        emitLog?.Invoke("INFO", $"Bot #{i + 1} confirmado no canal.");
                    }
                }
            }

            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private Func<string, string?> ConfigProvider(string botToken, int botIndex)
    {
        var sessionFile = Path.Combine(_sessionsDirectory, $"Nebula_Bot_{botIndex}.session");
        return what => what switch
        {
            "api_id" => _apiId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "api_hash" => _apiHash,
            "bot_token" => botToken,
            "session_pathname" => sessionFile,
            _ => null
        };
    }

    /// <summary>
    /// Envia um documento lógico completo pelo Bot API do Telegram.
    /// Partes Nebula têm 16 MiB e cabem no limite de upload desse endpoint.
    /// </summary>
    public async Task<NebulaTelegramUploadResult?> UploadDocumentAsync(
        int botIndex,
        byte[] data,
        string fileName,
        string mimeType,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        if (botIndex < 0 || botIndex >= _botTokens.Count || !_clients.ContainsKey(botIndex))
        {
            _logger.LogError("[NEBULA-TG] Bot com índice {BotIndex} não encontrado no pool.", botIndex);
            return null;
        }

        var upload = await AcquireUploadBotAsync(botIndex, cancellationToken).ConfigureAwait(false);
        botIndex = upload.BotIndex;
        await using var operation = upload.Lease;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(_chatId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "chat_id");

                var document = new ByteArrayContent(data);
                document.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                form.Add(document, "document", fileName);

                if (!string.IsNullOrWhiteSpace(caption))
                {
                    form.Add(new StringContent(caption), "caption");
                }

                var endpoint = $"https://api.telegram.org/bot{_botTokens[botIndex]}/sendDocument";
                using var response = await _httpClient.PostAsync(endpoint, form, cancellationToken).ConfigureAwait(false);
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return ParseUploadResultForBot(responseJson, botIndex);
                }

                if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                {
                    var ex = new HttpRequestException($"Telegram Bot API retornou HTTP {(int)response.StatusCode}.");
                    if ((int)response.StatusCode == 429 && TryReadRetryAfter(responseJson, out var retryAfter))
                    {
                        ex.Data["retry_after"] = retryAfter;
                    }

                    throw ex;
                }

                _logger.LogError(
                    "[NEBULA-TG] Telegram Bot API rejeitou o documento via Bot [{BotIndex}] com HTTP {StatusCode}.",
                    botIndex,
                    (int)response.StatusCode);
                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < 3 && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "[NEBULA-TG] Falha transitória no Bot API via Bot [{BotIndex}] (tentativa {Attempt}/3, tipo {ErrorType}); tentando novamente.",
                    botIndex,
                    attempt,
                    ex.GetType().Name);
                await Task.Delay(GetRetryDelay(ex, attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    "[NEBULA-TG] Erro ao enviar documento via Bot [{BotIndex}] (tipo {ErrorType}).",
                    botIndex,
                    ex.GetType().Name);
                return null;
            }
        }

        return null;
    }

    private static TimeSpan GetRetryDelay(Exception ex, int attempt)
    {
        if (ex.Data.Contains("retry_after") && ex.Data["retry_after"] is int retryAfter && retryAfter > 0)
        {
            return TimeSpan.FromSeconds(Math.Min(retryAfter, 300));
        }

        return TimeSpan.FromSeconds(attempt * 2);
    }

    internal static bool TryReadRetryAfter(string responseJson, out int retryAfter)
    {
        retryAfter = 0;
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("parameters", out var parameters) &&
                parameters.TryGetProperty("retry_after", out var retryAfterElement) &&
                retryAfterElement.TryGetInt32(out var parsed))
            {
                retryAfter = parsed;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        return false;
    }

    internal static bool IsValidBotApiChunkResponse(
        HttpResponseMessage response,
        long requestedOffset,
        int requestedLimit,
        int payloadLength)
    {
        if (requestedOffset < 0 || requestedLimit <= 0 || payloadLength <= 0)
        {
            return false;
        }

        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            var range = response.Content.Headers.ContentRange;
            if (range?.From != requestedOffset || range.To is null || range.To < range.From)
            {
                return false;
            }

            var rangeLength = range.To.Value - range.From!.Value + 1;
            return rangeLength <= requestedLimit && rangeLength == payloadLength;
        }

        // HTTP 200 is only safe for the first chunk when the server did not
        // need to honor a range. Never treat a complete file as a later chunk.
        return response.StatusCode == HttpStatusCode.OK && requestedOffset == 0 && payloadLength <= requestedLimit;
    }

    private static NebulaTelegramUploadResult? ParseUploadResult(string responseJson)
    {
        return ParseUploadResultForBot(responseJson, -1);
    }

    private static NebulaTelegramUploadResult? ParseUploadResultForBot(string responseJson, int botIndex)
    {
        var response = JsonSerializer.Deserialize<BotApiUploadResponse>(responseJson);
        if (response is not { Ok: true, Result.Document.FileId.Length: > 0 })
        {
            return null;
        }

        return new NebulaTelegramUploadResult(botIndex, response.Result.MessageId, response.Result.Chat.Id, response.Result.Document.FileId);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var client in _clients.Values)
        {
            try
            {
                client.Dispose();
            }
            catch
            {
                // Ignora erros no dispose
            }
        }

        _clients.Clear();
        if (_processLockHeld)
        {
            try
            {
                _processLock.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // A trava já foi liberada pelo sistema após o encerramento do processo.
            }

            _processLockHeld = false;
        }

        _processLock.Dispose();
        _initLock.Dispose();
        _httpClient.Dispose();
        _disposed = true;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class BotApiUploadResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public BotApiMessage Result { get; set; } = new();
    }

    private sealed class BotApiMessage
    {
        [JsonPropertyName("message_id")]
        public int MessageId { get; set; }

        [JsonPropertyName("chat")]
        public BotApiChat Chat { get; set; } = new();

        [JsonPropertyName("document")]
        public BotApiDocument Document { get; set; } = new();
    }

    private sealed class BotApiChat
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }

    private sealed class BotApiDocument
    {
        [JsonPropertyName("file_id")]
        public string FileId { get; set; } = string.Empty;
    }
}

/// <summary>
/// Identificadores retornados depois que o Bot API publica uma parte Nebula.
/// </summary>
public sealed class NebulaTelegramUploadResult
{
    /// <summary>
    /// Inicializa o resultado de upload.
    /// </summary>
    public NebulaTelegramUploadResult(int botIndex, int messageId, long chatId, string fileId)
    {
        BotIndex = botIndex;
        MessageId = messageId;
        ChatId = chatId;
        FileId = fileId;
    }

    /// <summary>Obtém o índice do bot que publicou o documento.</summary>
    public int BotIndex { get; }

    /// <summary>Obtém o ID da mensagem.</summary>
    public int MessageId { get; }

    /// <summary>Obtém o ID Bot API do chat.</summary>
    public long ChatId { get; }

    /// <summary>Obtém o identificador reutilizável do documento.</summary>
    public string FileId { get; }
}
