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
    private readonly Func<IEnumerable<string>>? _getStagingRoots;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    /// <summary>
    /// Obtém um valor que indica se o arquivo de staging local deve ser excluído após o upload concluído.
    /// </summary>
    public bool DeleteSourceAfterUpload => _deleteSourceAfterUpload;

    /// <summary>
    /// Indica se o arquivo é conteúdo protegido do cache local (capa, imagem,
    /// NFO/XML de metadados ou legenda), que nunca é excluído do disco.
    /// </summary>
    /// <param name="path">Caminho do arquivo.</param>
    /// <returns><see langword="true"/> para conteúdo protegido.</returns>
    internal static bool IsMetadataOrSidecar(string path) => NebulaProtectedContent.IsProtectedPath(path);

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
    /// <param name="getStagingRoots">Callback opcional para obter as pastas raiz de staging.</param>
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
        Func<string, Task>? logQueueState = null,
        Func<IEnumerable<string>>? getStagingRoots = null)
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
        _getStagingRoots = getStagingRoots;
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

    /// <summary>
    /// Marcador explícito de episódio no nome do arquivo (S01E02, S1.E2, S01_E02, S01-E02).
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex SeasonEpisodePattern = new(
        @"(?i)\bS\d{1,2}[ ._-]*E\d{1,3}\b",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Marcador compacto de episódio no nome do arquivo (2x01), típico de séries e novelas.
    /// Só vale quando o nome não carrega um ano de lançamento entre parênteses.
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex CompactEpisodePattern = new(
        @"(?i)\b\d{1,2}x\d{1,3}\b",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Ano entre parênteses: assinatura de título de filme ("O Rei do Show (2017)").
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex MovieYearPattern = new(
        @"\((?:19|20)\d{2}\)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Segmentos de pasta que declaram mídia de filme.
    /// </summary>
    private static readonly HashSet<string> MovieRootSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "filmes", "filme", "movies", "movie"
    };

    /// <summary>
    /// Segmentos de pasta que declaram mídia de série.
    /// </summary>
    private static readonly HashSet<string> SeriesRootSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "series", "serie", "série"
    };

    /// <summary>
    /// Segmentos de pasta que declaram conteúdo adulto.
    /// </summary>
    private static readonly HashSet<string> PornRootSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "porno", "porn", "xxx", "hentai", "adulto", "adult", "erotico", "erótico"
    };

    /// <summary>
    /// Segmentos que são pastas-raiz de categoria ou artefatos de montagem, nunca nomes de mídia.
    /// </summary>
    private static readonly HashSet<string> CategoryRootSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "filmes", "filme", "movies", "movie",
        "series", "serie", "série",
        "porno", "porn", "adulto", "hentai", "erotico", "erótico", "xxx",
        "strm", "nebula"
    };

    /// <summary>
    /// Segmentos de diretório que indicam série (Season 03, Temporada 2, Anime, Novela, Dorama).
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex SeriesFolderSegmentPattern = new(
        @"(?i)^(season|temporada|anime|novela|dorama|series?|série)\s*\d{0,2}$",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Divide um caminho relativo em segmentos, aceitando separadores Windows e POSIX.
    /// </summary>
    internal static string[] SplitPathSegments(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Normaliza os segmentos de um diretório relativo: remove artefatos de montagem
    /// ('strm', 'nebula') e as pastas-raiz de categoria do início do caminho.
    /// Assim a mídia nunca é roteada para uma raiz dentro da própria raiz
    /// (ex.: 'Series\Series\BoJack Horseman' vira 'BoJack Horseman').
    /// </summary>
    /// <param name="relativeDir">Diretório relativo original.</param>
    /// <returns>Segmentos de diretório já normalizados.</returns>
    internal static List<string> NormalizeMediaPathSegments(string? relativeDir)
        => SplitPathSegments(relativeDir)
            .Where(segment => !string.Equals(segment, "strm", StringComparison.OrdinalIgnoreCase)
                           && !string.Equals(segment, "nebula", StringComparison.OrdinalIgnoreCase))
            .SkipWhile(segment => CategoryRootSegments.Contains(segment))
            .ToList();

    /// <summary>
    /// Resolve a categoria declarada pela própria árvore de pastas. O segmento de categoria
    /// mais próximo da mídia vence (ex.: 'Series\Filmes\X' declara filme para o que está em X).
    /// </summary>
    /// <param name="segments">Segmentos do diretório relativo.</param>
    /// <returns>'FILME', 'SERIE', 'PORNO' ou null quando a pasta não declara categoria.</returns>
    internal static string? DeclaredCategoryFromPath(IEnumerable<string> segments)
    {
        string? declared = null;
        foreach (var segment in segments)
        {
            if (MovieRootSegments.Contains(segment))
            {
                declared = "FILME";
            }
            else if (SeriesRootSegments.Contains(segment))
            {
                declared = "SERIE";
            }
            else if (PornRootSegments.Contains(segment))
            {
                declared = "PORNO";
            }
        }

        return declared;
    }

    /// <summary>
    /// Classifica deterministicamente o tipo da mídia: 'SERIE', 'PORNO' ou 'FILME'.
    /// A árvore de pastas declarada manda mais que as palavras do título: um filme guardado
    /// em 'Series\Filmes\O Show dos Muppets (2026)' é FILME, um filme com ano no nome
    /// ('Temporada de Sangue (2025)') não vira série, e um título com 'Sex' ou 'Adult' no
    /// nome não vira Porno só por causa da palavra. Sem pasta declarada, valem os marcadores
    /// de episódio (S01E02, 2x01) e as palavras-chave de conteúdo adulto.
    /// </summary>
    public static string ClassifyMediaType(string? parent, string filename)
    {
        var parentValue = parent ?? string.Empty;
        var filenameValue = filename ?? string.Empty;
        var segments = SplitPathSegments(parentValue);
        var stem = Path.GetFileNameWithoutExtension(filenameValue);
        var immediateFolder = segments.Length > 0 ? segments[^1] : string.Empty;
        var hasMovieYear = MovieYearPattern.IsMatch(stem) || MovieYearPattern.IsMatch(immediateFolder);

        // 1. Marcador explícito de episódio no nome do arquivo vence qualquer pasta.
        if (SeasonEpisodePattern.IsMatch(stem))
        {
            return "SERIE";
        }

        // 2. Categoria declarada pela própria árvore de pastas.
        var declared = DeclaredCategoryFromPath(segments);
        if (declared == "SERIE" && hasMovieYear)
        {
            // Título com ano de lançamento dentro de uma raiz de séries é filme.
            declared = "FILME";
        }

        if (declared != null)
        {
            return declared;
        }

        // 3. Sem categoria declarada, valem as palavras-chave.
        if (AdultPathPattern.IsMatch(parentValue) || AdultFilenamePattern.IsMatch(filenameValue))
        {
            return "PORNO";
        }

        if (hasMovieYear)
        {
            return "FILME";
        }

        if (CompactEpisodePattern.IsMatch(stem))
        {
            return "SERIE";
        }

        if (segments.Any(segment => SeriesFolderSegmentPattern.IsMatch(segment)))
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
    /// Toda mídia termina sob exatamente uma raiz de categoria, independente da árvore
    /// de origem ter vindo de outra raiz (ex.: filmes guardados dentro de 'Series\Filmes').
    /// </summary>
    public static string RouteMediaRelativeDirectory(string? relativeDir, string filename)
    {
        var mediaType = ClassifyMediaType(relativeDir, filename);

        if (mediaType == "PORNO")
        {
            // Porno vai diretamente na pasta raiz 'Porno', achatando quaisquer subpastas
            return "Porno";
        }

        var root = mediaType == "SERIE" ? "Series" : "Filmes";
        var segments = NormalizeMediaPathSegments(relativeDir);

        return segments.Count == 0 ? root : $"{root}/{string.Join('/', segments)}";
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

            // 0. Verifica se esta mídia já foi enviada e concluída no Telegram (evita duplicata universal)
            var alreadyCompleted = _mongoContext != null
                ? await _mongoContext.FindCompletedMediaAsync(targetFileName, localFilePath, cancellationToken).ConfigureAwait(false)
                : null;

            if (alreadyCompleted != null)
            {
                var compName = alreadyCompleted.GetValue("name", targetFileName).AsString;
                var compId = alreadyCompleted.GetValue("_id").ToString();
                var protectedFile = IsMetadataOrSidecar(localFilePath);
                if (protectedFile)
                {
                    _logger.LogInformation(
                        "[NEBULA-UPLOAD] Capa/metadado '{Target}' já enviado com a mídia '{Comp}' (ID: {Id}); arquivo retirado do staging.",
                        targetFileName,
                        compName,
                        compId);
                    LogServer("INFO", $"[NEBULA-UPLOAD] '{targetFileName}' já enviado com a mídia. Arquivo retirado do staging.");
                }
                else
                {
                    _logger.LogInformation(
                        "[NEBULA-UPLOAD] Mídia '{Target}' já foi enviada para o Telegram anteriormente (Registro '{Comp}', ID: {Id}). Excluindo arquivo local para evitar duplicata.",
                        targetFileName,
                        compName,
                        compId);
                    LogServer("INFO", $"[NEBULA-UPLOAD] Mídia '{targetFileName}' já enviada ao Telegram anteriormente. Arquivo local excluído.");
                }

                try
                {
                    if (File.Exists(localFilePath))
                    {
                        File.Delete(localFilePath);
                        _logger.LogInformation("[NEBULA-UPLOAD] Arquivo local de mídia já enviada removido: {Path}", localFilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NEBULA-UPLOAD] Não foi possível remover arquivo local já enviado: {Path}", localFilePath);
                }

                NebulaMetadataExportService.ReleasePendingMarkerForMedia(localFilePath);
                CleanEmptyParentDirectories(localFilePath);
                return true;
            }

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
                        try
                        {
                            File.Delete(localFilePath);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[NEBULA-UPLOAD] Não foi possível remover staging file já concluído: {Path}", localFilePath);
                        }
                    }

                    // A recognition marker may have been created before this
                    // retry discovered that the media was already complete.
                    // Release it so queued sidecars are not blocked forever.
                    NebulaMetadataExportService.ReleasePendingMarkerForMedia(localFilePath);
                    if (_deleteSourceAfterUpload)
                    {
                        CleanEmptyParentDirectories(localFilePath);
                    }

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

            if (_deleteSourceAfterUpload)
            {
                CleanEmptyParentDirectories(localFilePath);
            }

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

    /// <summary>
    /// Limpa recursivamente diretórios pais vazios a partir do diretório do arquivo excluído,
    /// até atingir uma das raízes de staging configuradas ou a raiz da unidade de disco.
    /// </summary>
    internal void CleanEmptyParentDirectories(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            var currentDir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(currentDir) || !Directory.Exists(currentDir))
            {
                return;
            }

            var roots = _getStagingRoots?.Invoke()?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(Path.GetFullPath).ToList() ?? new List<string>();
            var isImmediateParent = true;

            while (!string.IsNullOrWhiteSpace(currentDir) && Directory.Exists(currentDir))
            {
                var fullCurrentDir = Path.GetFullPath(currentDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                var isRoot = roots.Any(root => string.Equals(
                    fullCurrentDir,
                    root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase));

                var pathRoot = Path.GetPathRoot(fullCurrentDir)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (isRoot || string.Equals(pathRoot, fullCurrentDir, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (roots.Count == 0 && !isImmediateParent)
                {
                    break;
                }

                isImmediateParent = false;

                if (NebulaMetadataExportService.IsOrphanPendingMarkerDirectory(currentDir))
                {
                    NebulaMetadataExportService.RemovePendingMarker(currentDir);
                }

                var allFiles = Directory.EnumerateFiles(currentDir, "*", SearchOption.AllDirectories).ToList();
                if (allFiles.All(f => string.Equals(Path.GetFileName(f), NebulaMetadataExportService.PendingMarkerFileName, StringComparison.OrdinalIgnoreCase)))
                {
                    NebulaMetadataExportService.RemovePendingMarker(currentDir);
                    allFiles.Clear();
                }

                if (allFiles.Count == 0 && !Directory.EnumerateDirectories(currentDir).Any())
                {
                    Directory.Delete(currentDir, true);
                    _logger.LogInformation("[NEBULA-UPLOAD] Diretório de staging vazio removido: {Dir}", currentDir);
                    LogServer("INFO", $"[NEBULA-UPLOAD] Diretório de staging vazio removido: {currentDir}");
                    currentDir = Path.GetDirectoryName(currentDir);
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-UPLOAD] Não foi possível verificar/remover diretórios vazios para {Path}", filePath);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
