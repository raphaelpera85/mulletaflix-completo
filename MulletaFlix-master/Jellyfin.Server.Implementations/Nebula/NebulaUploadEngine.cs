#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CA1001 // Types that own disposable fields should be disposable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Motor de upload concorrente e orquestração de arquivos para o Telegram no Nebula nativo em C#.
/// </summary>
public sealed class NebulaUploadEngine : IAsyncDisposable, IDisposable
{
    private const int MaxBotsPerMedia = 3;
    private readonly ILogger<NebulaUploadEngine> _logger;
    private readonly NebulaMongoContext _mongoContext;
    private readonly NebulaTelegramPool _telegramPool;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly int _logicalChunkSizeBytes;
    private readonly bool _deleteSourceAfterUpload;
    private readonly Action<string>? _uiLog;
    private readonly Func<BsonDocument, Task>? _onNodeUpdated;
    private readonly Action<string, string>? _emitServerLog;
    private readonly Func<string, Task>? _logQueueState;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>
    /// Obtém um valor que indica se o arquivo de staging local deve ser excluído após o upload concluído.
    /// </summary>
    public bool DeleteSourceAfterUpload => _deleteSourceAfterUpload;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaUploadEngine"/>.
    /// </summary>
    /// <param name="mongoContext">Contexto do MongoDB.</param>
    /// <param name="telegramPool">Pool de bots Telegram.</param>
    /// <param name="uploadConcurrency">Concorrência máxima de uploads simultâneos.</param>
    /// <param name="chunkSizeMb">Tamanho do chunk em MB.</param>
    /// <param name="deleteSourceAfterUpload">Indica se deve deletar arquivo local após envio.</param>
    /// <param name="logger">Instância de logger.</param>
    /// <param name="uiLog">Callback de log para interface do usuário.</param>
    /// <param name="onNodeUpdated">Callback opcional acionado quando um nó é concluído para sincronização imediata com Supabase.</param>
    /// <param name="emitServerLog">Callback de emissão de logs formatados do servidor.</param>
    /// <param name="logQueueState">Callback de log de estado da fila.</param>
    public NebulaUploadEngine(
        NebulaMongoContext mongoContext,
        NebulaTelegramPool telegramPool,
        int uploadConcurrency,
        int chunkSizeMb,
        bool deleteSourceAfterUpload,
        ILogger<NebulaUploadEngine> logger,
        Action<string>? uiLog = null,
        Func<BsonDocument, Task>? onNodeUpdated = null,
        Action<string, string>? emitServerLog = null,
        Func<string, Task>? logQueueState = null)
    {
        _mongoContext = mongoContext;
        _telegramPool = telegramPool;
        // Keep Nebula's storage format at 16 MiB per Telegram document. WTelegram
        // handles the smaller MTProto transport parts internally.
        _logicalChunkSizeBytes = 16 * 1024 * 1024;
        _ = chunkSizeMb;
        _deleteSourceAfterUpload = deleteSourceAfterUpload;
        _logger = logger;
        _uiLog = uiLog;
        _onNodeUpdated = onNodeUpdated;
        _emitServerLog = emitServerLog;
        _logQueueState = logQueueState;
        _concurrencySemaphore = new SemaphoreSlim(uploadConcurrency > 0 ? uploadConcurrency : 8);
    }

    private void LogServer(string level, string message)
    {
        _logger.LogInformation("[NEBULA-UPLOAD] {Message}", message);
        if (_emitServerLog != null)
        {
            _emitServerLog(level, message);
        }
        else
        {
            _uiLog?.Invoke(message);
        }
    }

    private static readonly System.Text.RegularExpressions.Regex AdultPathPattern = new(
        @"(?i)(?:^|[\\/\s._()+-])(porno|porn|xxx|hentai|adulto|adult|erotico|erótico|sexo|sex|18\+|\+18)(?=$|[\\/\s._()+-])",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex AdultFilenamePattern = new(
        @"(?i)(?:^|[\\/\s._()+-])(porno|porn|xxx|hentai|adulto|adult)(?=$|[\\/\s._()+-])",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex SeriesPattern = new(
        @"(?i)\bS\d{1,2}[ ._-]*E\d{1,3}\b|\b\d{1,2}x\d{1,3}\b|(?:^|[\\/\s._-])(series?|season|temporada|anime|novela|dorama|show)(?=$|[\\/\s._-])",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Classifica deterministicamente o tipo da mídia: 'SERIE', 'PORNO' ou 'FILME'.
    /// </summary>
    public static string ClassifyMediaType(string? parent, string filename)
    {
        var parentValue = parent ?? string.Empty;
        var filenameValue = filename ?? string.Empty;

        if (AdultPathPattern.IsMatch(parentValue) || AdultFilenamePattern.IsMatch(filenameValue))
        {
            return "PORNO";
        }

        if (SeriesPattern.IsMatch(parentValue) || SeriesPattern.IsMatch(filenameValue))
        {
            return "SERIE";
        }

        return "FILME";
    }

    /// <summary>
    /// Roteia e padroniza a estrutura de diretórios relativos para cada tipo de mídia:
    /// - FILMES: ficam sob a pasta raiz 'Filmes'
    /// - SERIES: ficam sob a pasta raiz 'Series', preservando todas as suas subpastas (ex: Temporada, Série)
    /// - PORNO: ficam diretamente na pasta raiz 'Porno', sem subpastas.
    /// </summary>
    public static string RouteMediaRelativeDirectory(string? relativeDir, string filename)
    {
        var mediaType = ClassifyMediaType(relativeDir, filename);

        if (mediaType == "PORNO")
        {
            // Porno vai diretamente na pasta raiz 'Porno', achatando quaisquer subpastas
            return "Porno";
        }

        var rawParts = (relativeDir ?? string.Empty)
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.Equals(p, "strm", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (mediaType == "SERIE")
        {
            if (rawParts.Count == 0)
            {
                return "Series";
            }

            var firstPartLower = rawParts[0].ToLowerInvariant();
            if (firstPartLower is "series" or "serie" or "temporada" or "season" or "anime" or "novela" or "dorama" or "show")
            {
                rawParts[0] = "Series";
            }
            else
            {
                rawParts.Insert(0, "Series");
            }

            return string.Join('/', rawParts);
        }

        // Caso FILME
        if (rawParts.Count == 0)
        {
            return "Filmes";
        }

        var first = rawParts[0].ToLowerInvariant();
        if (first is "filmes" or "filme" or "movies" or "movie")
        {
            rawParts[0] = "Filmes";
        }
        else
        {
            rawParts.Insert(0, "Filmes");
        }

        return string.Join('/', rawParts);
    }

    /// <summary>
    /// Gera a legenda estruturada do Telegram para visualização e reconstrução a partir do canal.
    /// Exemplo: [NEBULA] TIPO: FILME | MIDIA: Gosto Infernal (2025).mkv | PARTE: 80/110 | UUID: 793b8f4e-814d-4a09-9076-8537856292f4 | TAM: 1757.2MB.
    /// </summary>
    public static string BuildPartCaption(string mediaType, string filename, int partNum, int totalParts, string fileUuid, long totalSize)
    {
        var sizeMb = totalSize > 0 ? (totalSize / (1024.0 * 1024.0)) : 0.0;
        return $"[NEBULA] TIPO: {mediaType} | MIDIA: {filename} | PARTE: {partNum + 1}/{totalParts} | UUID: {fileUuid} | TAM: {sizeMb.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}MB";
    }

    /// <summary>
    /// Processa o upload completo ou retomada de um arquivo local para o Telegram em partes.
    /// </summary>
    /// <param name="localFilePath">Caminho local do arquivo.</param>
    /// <param name="targetFileName">Nome do arquivo no destino.</param>
    /// <param name="parentId">ID da pasta pai.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>True se o upload foi concluído com sucesso.</returns>
    public Task<bool> ProcessFileUploadAsync(
        string localFilePath,
        string targetFileName,
        string? parentId = null,
        CancellationToken cancellationToken = default)
        => ProcessFileUploadAsync(localFilePath, targetFileName, parentId, 1, cancellationToken);

    /// <summary>
    /// Processa o upload completo ou retomada de um arquivo local para o Telegram em partes com identificador de worker.
    /// </summary>
    /// <param name="localFilePath">Caminho local do arquivo.</param>
    /// <param name="targetFileName">Nome do arquivo no destino.</param>
    /// <param name="parentId">ID da pasta pai.</param>
    /// <param name="workerId">Identificador do worker de envio.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>True se o upload foi concluído com sucesso.</returns>
    public async Task<bool> ProcessFileUploadAsync(
        string localFilePath,
        string targetFileName,
        string? parentId,
        int workerId,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(localFilePath))
        {
            _logger.LogError("[NEBULA-UPLOAD] Arquivo local não encontrado: {Path}", localFilePath);
            return false;
        }

        await _concurrencySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var workerKey = workerId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var fileInfo = new FileInfo(localFilePath);
            var totalSize = fileInfo.Length;
            var totalParts = (int)Math.Ceiling((double)totalSize / _logicalChunkSizeBytes);
            if (totalParts <= 0)
            {
                totalParts = 1;
            }

            var mediaType = ClassifyMediaType(parentId, targetFileName);
            var parentValue = string.IsNullOrWhiteSpace(parentId)
                ? (BsonValue)BsonNull.Value
                : ObjectId.TryParse(parentId, out var parentObjectId)
                    ? parentObjectId
                    : parentId;

            // 1. Busca documento existente no MongoDB para verificar se já há partes enviadas
            var existingDoc = _mongoContext != null
                ? await _mongoContext.FindFileForUploadAsync(targetFileName, parentId, localFilePath, cancellationToken).ConfigureAwait(false)
                : null;

            var existingPartsMap = new Dictionary<int, NebulaFilePart>();
            string fileUuid;
            ObjectId nodeId;

            if (existingDoc != null)
            {
                nodeId = existingDoc.GetValue("_id").AsObjectId;

                // Se já estiver completado com todas as partes, não precisa reenviar
                var status = existingDoc.GetValue("status", string.Empty).AsString;
                if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) &&
                    IsCompletedUploadForFile(existingDoc, totalSize, totalParts))
                {
                    _logger.LogInformation("[NEBULA-UPLOAD] Arquivo '{Name}' já está 100% concluído no Nebula.", targetFileName);
                    if (_deleteSourceAfterUpload)
                    {
                        try { File.Delete(localFilePath); } catch { /* ignore */ }
                    }

                    // A recognition marker may have been created before this
                    // retry discovered that the media was already complete.
                    // Release it so queued sidecars are not blocked forever.
                    NebulaMetadataExportService.ReleasePendingMarkerForMedia(localFilePath);

                    return true;
                }

                var storedSize = existingDoc.TryGetValue("size", out var storedSizeValue) && storedSizeValue.IsNumeric
                    ? storedSizeValue.ToInt64()
                    : existingDoc.TryGetValue("file_size", out var legacySizeValue) && legacySizeValue.IsNumeric
                        ? legacySizeValue.ToInt64()
                        : -1L;
                var canResumeExistingParts = storedSize == totalSize;

                if (!canResumeExistingParts)
                {
                    _logger.LogWarning(
                        "[NEBULA-UPLOAD] Ignorando partes parciais de '{Name}': tamanho local={LocalSize}, tamanho persistido={StoredSize}.",
                        targetFileName,
                        totalSize,
                        storedSize);
                }

                // Carrega apenas partes compatíveis com o arquivo local atual.
                if (canResumeExistingParts && existingDoc.TryGetValue("parts", out var partsBson) && partsBson.IsBsonArray)
                {
                    foreach (var pElem in partsBson.AsBsonArray)
                    {
                        if (pElem is not BsonDocument pDoc)
                        {
                            continue;
                        }

                        var pNum = pDoc.Contains("part_number") ? pDoc.GetValue("part_number").ToInt32() : (pDoc.Contains("part_id") ? pDoc.GetValue("part_id").ToInt32() : -1);
                        var pSize = pDoc.Contains("size") ? pDoc.GetValue("size").ToInt64() : (pDoc.Contains("file_size") ? pDoc.GetValue("file_size").ToInt64() : 0L);
                        var fileId = pDoc.GetValue("tg_file_id", pDoc.GetValue("tg_file", string.Empty)).AsString;
                        var msgId = pDoc.GetValue("tg_message_id", pDoc.GetValue("tg_message", 0L)).ToInt64();
                        var chatId = pDoc.GetValue("tg_chat_id", 0L).ToInt64();
                        var botIdx = pDoc.GetValue("bot_index", 0).ToInt32();
                        var pStatus = pDoc.GetValue("status", "completed").AsString;
                        var uploadedAt = pDoc.GetValue("uploaded_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToInt64();
                        if (IsReusablePart(pNum, pSize, fileId, pStatus, totalSize, totalParts, _logicalChunkSizeBytes))
                        {
                            existingPartsMap[pNum] = new NebulaFilePart
                            {
                                PartNumber = pNum,
                                Size = pSize,
                                BotIndex = botIdx,
                                TgFileId = fileId,
                                TgMessageId = msgId,
                                TgChatId = chatId,
                                Status = pStatus,
                                UploadedAt = uploadedAt
                            };
                        }
                    }
                }

                // Reutiliza o UUID do arquivo já existente ou gera um novo
                if (canResumeExistingParts && existingDoc.Contains("file_uuid") && !string.IsNullOrWhiteSpace(existingDoc["file_uuid"].AsString))
                {
                    fileUuid = existingDoc["file_uuid"].AsString;
                }
                else
                {
                    fileUuid = Guid.NewGuid().ToString("D");
                }
            }
            else
            {
                nodeId = ObjectId.GenerateNewId();
                fileUuid = Guid.NewGuid().ToString("D");

                if (_mongoContext != null)
                {
                    var initialDoc = new BsonDocument
                    {
                        { "_id", nodeId },
                        { "name", targetFileName },
                        { "type", "file" },
                        { "is_directory", false },
                        { "status", "uploading" },
                        { "size", totalSize },
                        { "parent", parentValue },
                        { "local_path", localFilePath },
                        { "file_uuid", fileUuid },
                        { "media_type", mediaType },
                        { "parts", new BsonArray() },
                        { "queued_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                        { "created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                        { "modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                    };

                    await _mongoContext.InsertFileDocAsync(initialDoc, cancellationToken).ConfigureAwait(false);
                }
            }

            async Task<bool> FailUploadAsync(string reason)
            {
                if (_mongoContext != null)
                {
                    try
                    {
                        await _mongoContext.MarkUploadFailedAsync(nodeId, reason, cancellationToken, workerKey).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[NEBULA-UPLOAD] Não foi possível marcar '{Name}' como failed no MongoDB.", targetFileName);
                    }
                }

                return false;
            }

            // 2. Extrai partes contíguas a partir da parte 0
            var parts = new List<NebulaFilePart>();
            for (int i = 0; i < totalParts; i++)
            {
                if (existingPartsMap.TryGetValue(i, out var contiguousPart))
                {
                    parts.Add(contiguousPart);
                }
                else
                {
                    break;
                }
            }

            var resumePart = parts.Count;
            var sizeMb = totalSize / (1024.0 * 1024.0);
            var availableBots = _telegramPool?.GetAvailableBotIndices() ?? Array.Empty<int>();
            var botCount = availableBots.Count > 0 ? availableBots.Count : (_telegramPool?.BotCount ?? 27);

            if (_logQueueState != null)
            {
                await _logQueueState($"inicio:{targetFileName}").ConfigureAwait(false);
            }

            if (resumePart > 0)
            {
                var uploadedMb = parts.Sum(p => p.Size) / (1024.0 * 1024.0);
                LogServer("INFO", $"[W{workerId}] Retomando {targetFileName} na parte {resumePart}/{totalParts} ({uploadedMb:F2} MB ja enviados)");
            }
            else
            {
                LogServer("INFO", $"[W{workerId}] Iniciando upload: {targetFileName} tamanho={sizeMb.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} MB partes={totalParts} paralelo=1 bots={botCount}");
            }

            using (var fileStream = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true))
            {
                var buffer = new byte[_logicalChunkSizeBytes];
                for (int partNum = resumePart; partNum < totalParts; partNum++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var offset = (long)partNum * _logicalChunkSizeBytes;
                    fileStream.Seek(offset, SeekOrigin.Begin);

                    var remaining = totalSize - offset;
                    var bytesToRead = (int)Math.Min(_logicalChunkSizeBytes, remaining);
                    var bytesRead = 0;
                    while (bytesRead < bytesToRead)
                    {
                        var read = await fileStream.ReadAsync(buffer.AsMemory(bytesRead, bytesToRead - bytesRead), cancellationToken).ConfigureAwait(false);
                        if (read <= 0)
                        {
                            break;
                        }

                        bytesRead += read;
                    }

                    if (bytesRead <= 0)
                    {
                        break;
                    }

                    // Uma mídia pode usar até três bots. Isso permite distribuir
                    // partes entre bots sem deixar um único arquivo ocupar todo o pool.
                    availableBots = (_telegramPool?.GetAvailableBotIndices() ?? Array.Empty<int>())
                        .Take(MaxBotsPerMedia)
                        .ToArray();
                    if (availableBots.Count == 0)
                    {
                        _logger.LogError("[NEBULA-UPLOAD] Nenhum bot Telegram autenticado está disponível.");
                        return await FailUploadAsync("Nenhum bot Telegram autenticado disponível.").ConfigureAwait(false);
                    }

                    var caption = BuildPartCaption(mediaType, targetFileName, partNum, totalParts, fileUuid, totalSize);
                    var chunkName = $"{fileUuid}.part_{partNum:000}";

                    var chunkData = new byte[bytesRead];
                    Buffer.BlockCopy(buffer, 0, chunkData, 0, bytesRead);
                    NebulaTelegramUploadResult? tgMsg = null;
                    var botIndex = -1;
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    for (var botAttempt = 0; botAttempt < availableBots.Count; botAttempt++)
                    {
                        botIndex = availableBots[(partNum + botAttempt) % availableBots.Count];
                        LogServer("INFO", $"[UPLOAD] W{workerId} parte={partNum + 1} bot=#{botIndex + 1} iniciando");
                        tgMsg = await _telegramPool.UploadDocumentAsync(botIndex, chunkData, chunkName, "application/octet-stream", caption, cancellationToken).ConfigureAwait(false);
                        if (tgMsg != null)
                        {
                            break;
                        }

                        _logger.LogWarning("[NEBULA-UPLOAD] Bot [{Bot}] falhou ao publicar parte {Part}; tentando próximo bot disponível.", botIndex + 1, partNum + 1);
                    }

                    sw.Stop();

                    if (tgMsg == null)
                    {
                        _logger.LogError("[NEBULA-UPLOAD] Telegram não publicou a parte {Part} de '{Name}'.", partNum + 1, targetFileName);
                        LogServer("ERROR", $"[NEBULA-UPLOAD-ERRO] Telegram não publicou a parte {partNum + 1} de '{targetFileName}'.");
                        return await FailUploadAsync($"Telegram não publicou a parte {partNum + 1}.").ConfigureAwait(false);
                    }

                    // O pool pode escolher outro bot livre para manter o
                    // maior paralelismo possível; persiste o bot real usado.
                    botIndex = tgMsg.BotIndex;
                    var durationSec = sw.Elapsed.TotalSeconds;
                    var partMb = bytesRead / (1024.0 * 1024.0);
                    var speedMbS = durationSec > 0 ? partMb / durationSec : 0.0;

                    LogServer("INFO", $"[UPLOAD] W{workerId} parte={partNum + 1} bot=#{botIndex + 1} concluida em {durationSec.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}s ({speedMbS.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} MB/s)");

                    parts.Add(new NebulaFilePart
                    {
                        PartNumber = partNum,
                        Size = bytesRead,
                        BotIndex = botIndex,
                        TgFileId = tgMsg.FileId,
                        TgMessageId = tgMsg.MessageId,
                        TgChatId = tgMsg.ChatId,
                        Status = "completed",
                        UploadedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });

                    // 3. Atualiza progresso no MongoDB a cada parte enviada para persistir retomada
                    if (_mongoContext != null)
                    {
                        var currentPartDocs = new BsonArray();
                        foreach (var part in parts)
                        {
                            currentPartDocs.Add(new BsonDocument
                            {
                                { "part_number", part.PartNumber },
                                { "part_id", part.PartNumber },
                                { "size", part.Size },
                                { "file_size", part.Size },
                                { "bot_index", part.BotIndex },
                                { "tg_file_id", part.TgFileId },
                                { "tg_file", part.TgFileId },
                                { "tg_message_id", part.TgMessageId },
                                { "tg_message", part.TgMessageId },
                                { "tg_chat_id", part.TgChatId },
                                { "status", part.Status },
                                { "chunk_name", $"{fileUuid}.part_{part.PartNumber:000}" },
                                { "caption", BuildPartCaption(mediaType, targetFileName, part.PartNumber, totalParts, fileUuid, totalSize) },
                                { "uploaded_at", part.UploadedAt }
                            });
                        }

                        var uploadedBytes = parts.Sum(p => p.Size);
                        var ownsUpload = await _mongoContext.UpdateUploadProgressAsync(nodeId, currentPartDocs, uploadedBytes, botIndex, workerKey, cancellationToken).ConfigureAwait(false);
                        if (!ownsUpload)
                        {
                            _logger.LogWarning(
                                "[NEBULA-UPLOAD] Worker {Worker} perdeu a posse de '{Name}' durante o progresso; interrompendo para evitar sobrescrita.",
                                workerId,
                                targetFileName);
                            return false;
                        }
                    }
                }
            }

            if (parts.Count < totalParts)
            {
                _logger.LogError("[NEBULA-UPLOAD] Upload incompleto para '{Name}': {Count}/{Total} partes.", targetFileName, parts.Count, totalParts);
                return await FailUploadAsync($"Upload incompleto: {parts.Count}/{totalParts} partes.").ConfigureAwait(false);
            }

            var finalPartDocs = new BsonArray();
            foreach (var part in parts)
            {
                finalPartDocs.Add(new BsonDocument
                {
                    { "part_number", part.PartNumber },
                    { "part_id", part.PartNumber },
                    { "size", part.Size },
                    { "file_size", part.Size },
                    { "bot_index", part.BotIndex },
                    { "tg_file_id", part.TgFileId },
                    { "tg_file", part.TgFileId },
                    { "tg_message_id", part.TgMessageId },
                    { "tg_message", part.TgMessageId },
                    { "tg_chat_id", part.TgChatId },
                    { "status", part.Status },
                    { "chunk_name", $"{fileUuid}.part_{part.PartNumber:000}" },
                    { "caption", BuildPartCaption(mediaType, targetFileName, part.PartNumber, totalParts, fileUuid, totalSize) },
                    { "uploaded_at", part.UploadedAt }
                });
            }

            var firstPart = parts[0];
            if (_mongoContext != null)
            {
                var finalFields = new BsonDocument
                {
                    { "name", targetFileName },
                    { "type", "file" },
                    { "is_directory", false },
                    { "status", "completed" },
                    { "size", totalSize },
                    { "file_uuid", fileUuid },
                    { "media_type", mediaType },
                    { "parent", parentValue },
                    { "parts", finalPartDocs },
                    { "uploaded_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    { "modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    { "tg_message_id", firstPart.TgMessageId },
                    { "tg_chat_id", firstPart.TgChatId },
                    { "tg_file_id", firstPart.TgFileId }
                };

                var completed = await _mongoContext.CompleteFileUploadAsync(nodeId, finalFields, cancellationToken, workerKey).ConfigureAwait(false);
                if (!completed)
                {
                    _logger.LogWarning(
                        "[NEBULA-UPLOAD] Worker {Worker} perdeu a posse de '{Name}' antes da conclusão; não sobrescrevendo o estado.",
                        workerId,
                        targetFileName);
                    return false;
                }

                if (_onNodeUpdated != null)
                {
                    try
                    {
                        var syncDoc = finalFields.DeepClone().AsBsonDocument;
                        syncDoc["_id"] = nodeId;
                        await _onNodeUpdated(syncDoc).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[NEBULA-UPLOAD] Aviso ao sincronizar nó com o Supabase em tempo real.");
                    }
                }
            }

            LogServer("INFO", $"[W{workerId}] Concluido: {targetFileName}");

            if (_logQueueState != null)
            {
                await _logQueueState($"concluido:{targetFileName}").ConfigureAwait(false);
            }

            if (_deleteSourceAfterUpload)
            {
                try
                {
                    File.Delete(localFilePath);
                    _logger.LogInformation("[NEBULA-UPLOAD] Arquivo de staging removido: {Path}", localFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NEBULA-UPLOAD] Não foi possível remover staging file: {Path}", localFilePath);
                }
            }

            // O marcador é removido somente quando a mídia alcança o estado
            // completed. Um NFO/capa concluído nunca pode liberar os demais
            // sidecars antes da mídia correspondente.
            NebulaMetadataExportService.ReleasePendingMarkerForMedia(localFilePath);

            return true;
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    /// <inheritdoc />
    internal static bool IsCompletedUploadForFile(BsonDocument doc, long expectedSize, int expectedParts)
    {
        if (doc == null || expectedSize < 0 || expectedParts <= 0)
        {
            return false;
        }

        var storedSize = doc.Contains("size")
            ? doc.GetValue("size").ToInt64()
            : doc.GetValue("file_size", 0L).ToInt64();
        if (storedSize != expectedSize)
        {
            return false;
        }

        if (!doc.TryGetValue("parts", out var partsValue) || !partsValue.IsBsonArray)
        {
            return false;
        }

        var parts = partsValue.AsBsonArray
            .OfType<BsonDocument>()
            .OrderBy(part => part.Contains("part_number") ? part.GetValue("part_number").ToInt32() : part.GetValue("part_id", -1).ToInt32())
            .ToList();
        if (parts.Count != expectedParts)
        {
            return false;
        }

        long totalPartSize = 0;
        for (var i = 0; i < parts.Count; i++)
        {
            var part = parts[i];
            var partNumber = part.Contains("part_number") ? part.GetValue("part_number").ToInt32() : part.GetValue("part_id", -1).ToInt32();
            var partSize = part.Contains("size") ? part.GetValue("size").ToInt64() : part.GetValue("file_size", 0L).ToInt64();
            var fileId = part.GetValue("tg_file_id", part.GetValue("tg_file", string.Empty)).AsString;
            if (partNumber != i || partSize <= 0 || string.IsNullOrWhiteSpace(fileId))
            {
                return false;
            }

            totalPartSize += partSize;
        }

        return totalPartSize == expectedSize;
    }

    internal static bool IsReusablePart(
        int partNumber,
        long partSize,
        string? fileId,
        string? status,
        long totalSize,
        int totalParts,
        int logicalChunkSize)
    {
        if (partNumber < 0 || partNumber >= totalParts ||
            string.IsNullOrWhiteSpace(fileId) ||
            !string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) ||
            totalSize <= 0 || logicalChunkSize <= 0)
        {
            return false;
        }

        var expectedPartSize = Math.Min(logicalChunkSize, totalSize - ((long)partNumber * logicalChunkSize));
        return expectedPartSize > 0 && partSize == expectedPartSize;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _cts.Cancel();
        _concurrencySemaphore.Dispose();
        _cts.Dispose();
        _disposed = true;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
