#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CA1001 // Types that own disposable fields should be disposable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Motor nativo em C# para escaneamento de arquivos .strm, download multipart resiliente via Range requests,
/// deduplicação no MongoDB e alimentação direta de fila (Feeder) para envio ao Telegram.
/// </summary>
public sealed class NebulaDownloaderEngine : IAsyncDisposable, IDisposable
{
    internal static readonly HashSet<string> SupportedMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // STRM
        ".strm",
        // Video
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v", ".ts", ".webm",
        ".flv", ".vob", ".m2ts", ".3gp", ".ogv", ".mpg", ".mpeg", ".iso",
        ".rmvb", ".asf", ".divx", ".f4v",
        // Audio
        ".mp3", ".flac", ".aac", ".wav", ".m4a", ".ogg", ".wma", ".opus",
        ".alac", ".aiff", ".ape", ".ac3", ".eac3", ".dts"
    };

    internal static readonly HashSet<string> SupportedSidecarExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Legendas
        ".srt", ".sub", ".ass", ".ssa", ".vtt", ".smi", ".idx",
        // Metadados e NFO
        ".nfo", ".xml",
        // Imagens / Arte
        ".jpg", ".jpeg", ".png", ".webp", ".avif", ".tbn"
    };

    private static readonly string[] VideoExtensions =
    [
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v", ".ts", ".webm",
        ".flv", ".vob", ".m2ts", ".3gp", ".ogv", ".mpg", ".mpeg", ".iso",
        ".rmvb", ".asf", ".divx", ".f4v"
    ];
    internal static readonly Regex EpisodeRegex = new(@"(?i)(?<prefix>.*?)(?:[.\s_-]+)?s(?<season>\d{1,2})[.\s_-]*e(?<episode>\d{1,3})", RegexOptions.Compiled);
    internal static readonly Regex YearRegex = new(@"(?<!\d)((?:19|20)\d{2})(?!\d)", RegexOptions.Compiled);

    private readonly NebulaMongoContext _mongoContext;
    private readonly NebulaTelegramPool _telegramPool;
    private readonly ILogger<NebulaDownloaderEngine> _logger;
    private readonly NebulaMetadataExportService? _metadataExportService;
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly FailureTracker _failureTracker = new();

    private Task? _workerTask;
    private bool _isRunning;

    /// <summary>
    /// Evento disparado para atualizar o status do download em andamento.
    /// </summary>
    public event Action<NebulaDownloadStatusDto>? OnProgressChanged;

    /// <summary>
    /// Evento disparado para enviar logs em tempo real para a interface web.
    /// </summary>
    public event Action<string>? OnLog;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaDownloaderEngine"/>.
    /// </summary>
    public NebulaDownloaderEngine(
        NebulaMongoContext mongoContext,
        NebulaTelegramPool telegramPool,
        ILogger<NebulaDownloaderEngine> logger,
        NebulaMetadataExportService? metadataExportService = null)
    {
        _mongoContext = mongoContext;
        _telegramPool = telegramPool;
        _logger = logger;
        _metadataExportService = metadataExportService;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 64,
            EnableMultipleHttp2Connections = true,
            ResponseDrainTimeout = TimeSpan.FromSeconds(15)
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "VLC/3.0.21 LibVLC/3.0.21");
    }

    /// <summary>
    /// Obtém se o motor de download está em execução.
    /// </summary>
    public bool IsRunning => _isRunning;

    private void LogInfo(string message)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        OnLog?.Invoke($"[{ts}][INFO][STRM] {message}");
    }

    private void LogError(string message)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        OnLog?.Invoke($"[{ts}][ERROR][STRM] {message}");
    }

    private void LogWarning(string message)
    {
        var ts = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        OnLog?.Invoke($"[{ts}][WARNING][STRM] {message}");
    }

    /// <summary>
    /// Inicia o processo de escaneamento e download de .strm em background.
    /// </summary>
    public void Start(NebulaFtpConfiguration config)
    {
        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _workerTask = Task.Run(() => WorkerLoopAsync(config, _cts.Token));
    }

    /// <summary>
    /// Interrompe o processo de download de forma limpa.
    /// </summary>
    public async Task StopAsync()
    {
        _isRunning = false;
        _cts.Cancel();
        if (_workerTask != null)
        {
            try
            {
                await _workerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEBULA-DOWNLOADER] Erro ao aguardar encerramento do worker.");
            }
        }

        OnProgressChanged?.Invoke(new NebulaDownloadStatusDto
        {
            Name = "Downloader parado.",
            Percentage = 0,
            DetailText = "0.0%"
        });
        LogInfo("STRM Downloader encerrado.");
    }

    private async Task WorkerLoopAsync(NebulaFtpConfiguration config, CancellationToken cancellationToken)
    {
        var monitorSources = (config.MonitorPaths ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
            .ToList();

        // Garante inclusão de D:\midias e D:\midias2 se existirem no sistema e não estiverem na lista
        foreach (var defaultPath in new[] { @"D:\midias", @"D:\midias2" })
        {
            if (Directory.Exists(defaultPath) && !monitorSources.Any(s => string.Equals(Path.GetFullPath(s), Path.GetFullPath(defaultPath), StringComparison.OrdinalIgnoreCase)))
            {
                monitorSources.Add(defaultPath);
            }
        }

        if (monitorSources.Count == 0)
        {
            LogError("Nenhuma pasta de monitoramento válida configurada em MonitorPaths.");
            _isRunning = false;
            return;
        }

        var stageRoots = (config.StagePaths ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (stageRoots.Count == 0)
        {
            stageRoots.Add(Path.Combine(AppContext.BaseDirectory, "NebulaStage"));
        }

        LogInfo("=== Downloader & Feeder de Mídias para NebulaFTP ===");
        LogInfo($"Pastas de Stage detectadas: [{string.Join(", ", stageRoots.Select(r => $"'{r.Replace(@"\", @"\\", StringComparison.Ordinal)}'"))}]");
        LogInfo($"Fontes de Mídias Monitoradas: [{string.Join(", ", monitorSources.Select(s => $"'{s.Replace(@"\", @"\\", StringComparison.Ordinal)}'"))}]");

        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                // 1. Limpeza preventiva de diretórios vazios em todas as fontes monitoradas
                foreach (var src in monitorSources)
                {
                    CleanAllEmptySubdirectories(src);
                }

                LogInfo("Escaneando e priorizando mídias compatíveis (.strm, .mkv, .mp4, etc.)...");
                OnProgressChanged?.Invoke(new NebulaDownloadStatusDto
                {
                    Name = "Preparando fila do Downloader & Feeder...",
                    StageStep = "Escaneando mídias...",
                    DetailText = "Aguarde; a fila está sendo priorizada.",
                    Percentage = 0
                });

                var mediaFiles = new List<string>();
                foreach (var src in monitorSources)
                {
                    try
                    {
                        if (!Directory.Exists(src))
                        {
                            continue;
                        }

                        var allCandidateFiles = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
                            .Where(f =>
                            {
                                var name = Path.GetFileName(f);
                                if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.'))
                                {
                                    return false;
                                }

                                if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
                                    name.EndsWith(".download", StringComparison.OrdinalIgnoreCase) ||
                                    name.Contains(".part", StringComparison.OrdinalIgnoreCase))
                                {
                                    return false;
                                }

                                return true;
                            })
                            .ToList();

                        var mediaList = allCandidateFiles
                            .Where(f => SupportedMediaExtensions.Contains(Path.GetExtension(f)))
                            .ToList();
                        mediaFiles.AddRange(mediaList);

                        // Limpeza de arquivos sidecar órfãos (.nfo, .xml, capas, etc.) cuja mídia já foi enviada e concluída no Telegram
                        var orphanSidecars = allCandidateFiles
                            .Where(f => SupportedSidecarExtensions.Contains(Path.GetExtension(f)))
                            .ToList();
                        foreach (var sidecar in orphanSidecars)
                        {
                            try
                            {
                                var sidecarDir = Path.GetDirectoryName(sidecar);
                                var hasActiveMediaInDir = !string.IsNullOrEmpty(sidecarDir) &&
                                    Directory.EnumerateFiles(sidecarDir).Any(f => SupportedMediaExtensions.Contains(Path.GetExtension(f)));

                                if (!hasActiveMediaInDir && _mongoContext != null)
                                {
                                    var sidecarName = Path.GetFileName(sidecar);
                                    var completedDoc = await _mongoContext.FindCompletedMediaAsync(sidecarName, sidecar, cancellationToken).ConfigureAwait(false);
                                    if (completedDoc != null)
                                    {
                                        if (File.Exists(sidecar))
                                        {
                                            File.Delete(sidecar);
                                            LogInfo($"Arquivo {sidecarName} (já concluído no Telegram) removido de {sidecarDir}");
                                            CleanEmptyParentDirectoriesWithLog(sidecarDir, monitorSources);
                                        }
                                    }
                                }
                            }
                            catch (Exception scEx)
                            {
                                _logger.LogDebug(scEx, "[NEBULA-DOWNLOADER] Erro ao verificar sidecar {Path}", sidecar);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[NEBULA-DOWNLOADER] Erro ao escanear pasta {Dir}", src);
                    }
                }

                // Ordenar arquivos por categoria (Filmes -> Porno -> Series -> Outros) e por ano decrescente (2026 -> 2025 -> ...)
                var prioritizedList = mediaFiles
                    .Select(path => new
                    {
                        Path = path,
                        Category = GetCategoryPriority(path),
                        CategoryName = GetCategoryDisplayName(GetCategoryPriority(path)),
                        Year = ExtractMediaYear(Path.GetFileName(path), Path.GetDirectoryName(path) ?? string.Empty),
                        IsStrm = string.Equals(Path.GetExtension(path), ".strm", StringComparison.OrdinalIgnoreCase)
                    })
                    .OrderBy(x => x.Category) // 1. Filmes -> 2. Porno -> 3. Series -> 4. Outros
                    .ThenByDescending(x => x.Year) // Ano decrescente (2026 -> 2025 -> ...)
                    .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                LogInfo($"Total de mídias encontradas: {prioritizedList.Count}");
                if (prioritizedList.Count == 0)
                {
                    OnProgressChanged?.Invoke(new NebulaDownloadStatusDto
                    {
                        Name = "Nenhuma mídia pendente",
                        StageStep = "Aguardando novas mídias...",
                        DetailText = "0.0%",
                        Percentage = 0
                    });
                }

                foreach (var item in prioritizedList)
                {
                    if (cancellationToken.IsCancellationRequested || !_isRunning)
                    {
                        break;
                    }

                    if (_failureTracker.ShouldSkip(item.Path, out var skipReason))
                    {
                        _logger.LogDebug("[NEBULA-DOWNLOADER] Pulando mídia com falha recente: {Path} ({Reason})", item.Path, skipReason);
                        continue;
                    }

                    if (item.IsStrm)
                    {
                        await ProcessSingleStrmAsync(item.Path, item.CategoryName, item.Year, monitorSources, stageRoots, config, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await ProcessSinglePhysicalMediaAsync(item.Path, item.CategoryName, item.Year, monitorSources, stageRoots, config, cancellationToken).ConfigureAwait(false);
                    }
                }

                // 2. Limpeza final de subpastas que possam ter ficado vazias
                foreach (var src in monitorSources)
                {
                    CleanAllEmptySubdirectories(src);
                }

                // Aguarda 60 segundos antes de um novo ciclo de escaneamento
                await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[NEBULA-DOWNLOADER] Erro no loop de escaneamento/download.");
                LogError(ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ProcessSingleStrmAsync(
        string strmPath,
        string categoryName,
        int year,
        List<string> monitorSources,
        List<string> stageRoots,
        NebulaFtpConfiguration config,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(strmPath))
        {
            return;
        }

        var bestStageDir = SelectBestStageDirectory(stageRoots);
        var targetStageStrmDir = Path.Combine(bestStageDir, "strm");
        var yearPart = year > 0 ? $"/{year}" : string.Empty;
        var strmFileName = Path.GetFileName(strmPath);

        string url;
        try
        {
            url = await ReadStrmUrlAsync(strmPath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(url))
            {
                LogError($"Erro no download de {strmFileName}: Arquivo .strm vazio ou inválido.");
                _failureTracker.RecordFailure(strmPath, "Arquivo .strm vazio ou inválido");
                return;
            }
        }
        catch (Exception ex)
        {
            LogError($"Erro no download de {strmFileName}: {ex.Message}");
            _failureTracker.RecordFailure(strmPath, ex.Message);
            return;
        }

        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(strmPath);
        var extension = GuessExtensionFromUrl(url);
        var finalMediaFileName = $"{fileNameWithoutExt}{extension}";
        var rawRelPath = GetRelativePathFromSource(strmPath, monitorSources);
        var relPath = NebulaUploadEngine.RouteMediaRelativeDirectory(rawRelPath, finalMediaFileName);
        var targetStageDir = string.IsNullOrEmpty(relPath) ? targetStageStrmDir : Path.Combine(bestStageDir, relPath);
        var targetMediaFilePath = Path.Combine(targetStageDir, finalMediaFileName);

        // 1. Checa se o arquivo já está completado ou ativo no MongoDB por Título, Link ou ID
        var (isDuplicate, isCompletedDuplicate, reason) = await CheckMediaDuplicateInMongoAsync(strmPath, finalMediaFileName, url, cancellationToken).ConfigureAwait(false);
        if (isDuplicate)
        {
            if (!isCompletedDuplicate)
            {
                LogInfo($"Mídia já está ativa no Nebula ({reason}). Preservando .strm até a publicação ser concluída: {strmFileName}");
                return;
            }

            LogInfo($"Mídia já concluída no Nebula ({reason}). Removendo .strm e sidecars: {strmFileName}");
            _failureTracker.RecordSuccess(strmPath);

            // Recognition may have created a pending marker before the
            // duplicate was discovered. Remove only an orphan marker; an
            // active media payload keeps the marker until its upload completes.
            if (NebulaMetadataExportService.IsOrphanPendingMarkerDirectory(targetStageDir))
            {
                NebulaMetadataExportService.RemovePendingMarker(targetStageDir);
            }

            try
            {
                if (File.Exists(strmPath))
                {
                    File.Delete(strmPath);
                    LogInfo($"Arquivo de origem removido: {strmPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEBULA-DOWNLOADER] Erro ao deletar STRM duplicado {Path}", strmPath);
            }

            // Exclui arquivos sidecars (.nfo, .xml, .srt, imagens, etc.) associados na pasta de origem e no stage
            DeleteAssociatedSidecars(strmPath, fileNameWithoutExt);
            DeleteTargetStageDirectoryIfCompleted(targetStageDir);

            CleanEmptyParentDirectoriesWithLog(Path.GetDirectoryName(strmPath), monitorSources);
            return;
        }

        // 2. Log de início do download exclusivo da mídia
        LogInfo($"[1 MÍDIA POR VEZ] Iniciando download: {strmFileName} [{categoryName}{yearPart}] -> Stage: {targetStageStrmDir}");
        OnProgressChanged?.Invoke(new NebulaDownloadStatusDto
        {
            Name = strmFileName,
            StageStep = "Baixando mídia...",
            DetailText = "0.0%",
            Percentage = 0
        });

        Directory.CreateDirectory(targetStageDir);

        // 3. Executa o download multipart resiliente sem aguardar o reconhecimento
        // antecipado da mídia pelo Jellyfin.
        var partsCount = config.DownloadParts > 0 ? Math.Clamp(config.DownloadParts, 1, 32) : 24;
        bool downloadOk;
        try
        {
            downloadOk = await DownloadMultipartAsync(url, targetMediaFilePath, partsCount, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            NebulaMetadataExportService.RemovePendingMarker(targetStageDir);
            var err = ex.StatusCode.HasValue ? $"HTTP Error {(int)ex.StatusCode.Value}: {ex.StatusCode.Value}" : ex.Message;
            LogError($"Erro no download de {strmFileName}: {err}");
            _failureTracker.RecordFailure(strmPath, err);
            return;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            NebulaMetadataExportService.RemovePendingMarker(targetStageDir);
            var err = "A requisição foi cancelada porque o timeout configurado do HttpClient (10 minutos) foi atingido.";
            LogError($"Erro no download de {strmFileName}: {err}");
            _failureTracker.RecordFailure(strmPath, err);
            return;
        }
        catch (Exception ex)
        {
            NebulaMetadataExportService.RemovePendingMarker(targetStageDir);
            LogError($"Erro no download de {strmFileName}: {ex.Message}");
            _failureTracker.RecordFailure(strmPath, ex.Message);
            return;
        }

        if (!downloadOk || !File.Exists(targetMediaFilePath))
        {
            NebulaMetadataExportService.RemovePendingMarker(targetStageDir);
            return;
        }

        if (downloadOk && File.Exists(targetMediaFilePath))
        {
            _failureTracker.RecordSuccess(strmPath);

            // 4. Feeder: Registra no MongoDB com status 'queued' e delete_source=true
            try
            {
                await EnqueueFileInMongoAsync(targetMediaFilePath, relPath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                NebulaMetadataExportService.RemovePendingMarker(targetStageDir);
                _failureTracker.RecordFailure(strmPath, ex.Message);
                LogError($"Erro ao enfileirar mídia baixada {strmFileName}: {ex.Message}");
                return;
            }

            // 5. Remove o arquivo .strm de origem após o download/enfileiramento com sucesso
            try
            {
                if (File.Exists(strmPath))
                {
                    File.Delete(strmPath);
                    LogInfo($"Arquivo de origem removido: {strmPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEBULA-DOWNLOADER] Erro ao deletar .strm de origem {Path}", strmPath);
            }

            CleanEmptyParentDirectoriesWithLog(Path.GetDirectoryName(strmPath), monitorSources);
            LogInfo($"[1 MÍDIA POR VEZ] Conclusão do processamento de: {strmFileName}. Pronto para a próxima mídia.");
        }
    }

    private async Task ProcessSinglePhysicalMediaAsync(
        string mediaPath,
        string categoryName,
        int year,
        List<string> monitorSources,
        List<string> stageRoots,
        NebulaFtpConfiguration config,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(mediaPath))
        {
            return;
        }

        // 1. Verifica se o arquivo está pronto para leitura (não está sendo gravado/copiado)
        if (!await IsFileReadyAsync(mediaPath, cancellationToken).ConfigureAwait(false))
        {
            _logger.LogDebug("[NEBULA-DOWNLOADER] Arquivo em uso ou ainda sendo copiado: {Path}. Aguardando próximo ciclo.", mediaPath);
            return;
        }

        var mediaFileName = Path.GetFileName(mediaPath);
        var yearPart = year > 0 ? $"/{year}" : string.Empty;
        var rawRelPath = GetRelativePathFromSource(mediaPath, monitorSources);
        var relPath = NebulaUploadEngine.RouteMediaRelativeDirectory(rawRelPath, mediaFileName);

        // 2. Checa se o arquivo já está completado ou ativo no MongoDB
        var (isDuplicate, isCompletedDuplicate, reason) = await CheckMediaDuplicateInMongoAsync(mediaPath, mediaFileName, url: string.Empty, cancellationToken).ConfigureAwait(false);
        if (isDuplicate)
        {
            if (!isCompletedDuplicate)
            {
                LogInfo($"Mídia já está ativa no Nebula ({reason}). Preservando arquivo até a publicação ser concluída: {mediaFileName}");
                return;
            }

            LogInfo($"Mídia já concluída no Nebula ({reason}). Removendo da pasta de origem: {mediaFileName}");
            _failureTracker.RecordSuccess(mediaPath);

            try
            {
                if (File.Exists(mediaPath))
                {
                    File.Delete(mediaPath);
                    LogInfo($"Arquivo de origem duplicado removido: {mediaPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEBULA-DOWNLOADER] Erro ao deletar mídia duplicada {Path}", mediaPath);
            }

            // Exclui arquivos sidecars (.nfo, .xml, .srt, imagens, etc.) associados na pasta de origem
            var mediaStem = Path.GetFileNameWithoutExtension(mediaPath);
            DeleteAssociatedSidecars(mediaPath, mediaStem);

            CleanEmptyParentDirectoriesWithLog(Path.GetDirectoryName(mediaPath), monitorSources);
            return;
        }

        // 3. Determina o destino no Stage
        var fileInfo = new FileInfo(mediaPath);
        var totalBytes = fileInfo.Length;
        if (totalBytes <= 0)
        {
            LogError($"Arquivo de mídia vazio (0 bytes): {mediaFileName}");
            return;
        }

        var isAlreadyInStage = stageRoots.Any(sr => IsPathWithinRoot(mediaPath, sr));
        string targetMediaFilePath;
        string targetStageDir;

        if (isAlreadyInStage)
        {
            targetMediaFilePath = mediaPath;
            targetStageDir = Path.GetDirectoryName(mediaPath) ?? string.Empty;
        }
        else
        {
            var bestStageDir = SelectBestStageDirectory(stageRoots, requiredBytes: totalBytes);
            targetStageDir = string.IsNullOrEmpty(relPath) ? bestStageDir : Path.Combine(bestStageDir, relPath);
            targetMediaFilePath = Path.Combine(targetStageDir, mediaFileName);

            Directory.CreateDirectory(targetStageDir);

            LogInfo($"[1 MÍDIA POR VEZ] Movendo mídia física para Stage: {mediaFileName} [{categoryName}{yearPart}] -> {targetStageDir}");
            OnProgressChanged?.Invoke(new NebulaDownloadStatusDto
            {
                Name = mediaFileName,
                StageStep = "Movendo para pasta de envio (Stage)...",
                DetailText = FormatBytes(totalBytes),
                Percentage = 50,
                TotalMb = Math.Round(totalBytes / (1024.0 * 1024.0), 2),
                DoneMb = 0
            });

            try
            {
                await MoveOrCopyFileAsync(mediaPath, targetMediaFilePath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError($"Erro ao mover mídia física {mediaFileName} para o Stage: {ex.Message}");
                _failureTracker.RecordFailure(mediaPath, ex.Message);
                return;
            }

            // Move sidecars que estejam na mesma pasta da mídia de origem
            var parentDir = Path.GetDirectoryName(mediaPath);
            if (!string.IsNullOrWhiteSpace(parentDir) && Directory.Exists(parentDir) && !isAlreadyInStage)
            {
                try
                {
                    foreach (var sidecar in Directory.EnumerateFiles(parentDir))
                    {
                        var sidecarExt = Path.GetExtension(sidecar);
                        if (SupportedSidecarExtensions.Contains(sidecarExt))
                        {
                            var destSidecar = Path.Combine(targetStageDir, Path.GetFileName(sidecar));
                            try
                            {
                                if (!File.Exists(destSidecar))
                                {
                                    File.Move(sidecar, destSidecar, true);
                                    LogInfo($"Sidecar movido para Stage: {Path.GetFileName(sidecar)}");
                                }
                            }
                            catch (Exception scEx)
                            {
                                _logger.LogWarning(scEx, "[NEBULA-DOWNLOADER] Erro ao mover sidecar {Path}", sidecar);
                            }
                        }
                    }
                }
                catch (Exception scDirEx)
                {
                    _logger.LogWarning(scDirEx, "[NEBULA-DOWNLOADER] Erro ao enumerar sidecars em {Dir}", parentDir);
                }
            }
        }

        // 4. Registra no MongoDB com status 'queued' e delete_source=true
        if (File.Exists(targetMediaFilePath))
        {
            _failureTracker.RecordSuccess(mediaPath);
            try
            {
                await EnqueueFileInMongoAsync(targetMediaFilePath, relPath, cancellationToken).ConfigureAwait(false);
                LogInfo($"[NEBULA-FEEDER] Mídia física {mediaFileName} enfileirada no MongoDB com sucesso.");
            }
            catch (Exception ex)
            {
                LogError($"Erro ao enfileirar mídia física {mediaFileName}: {ex.Message}");
                _failureTracker.RecordFailure(mediaPath, ex.Message);
                return;
            }

            // Enfileira sidecars também no MongoDB para que subam juntos
            if (Directory.Exists(targetStageDir))
            {
                try
                {
                    foreach (var sidecar in Directory.EnumerateFiles(targetStageDir))
                    {
                        var sidecarExt = Path.GetExtension(sidecar);
                        if (SupportedSidecarExtensions.Contains(sidecarExt))
                        {
                            await EnqueueFileInMongoAsync(sidecar, relPath, cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[NEBULA-DOWNLOADER] Erro ao enfileirar sidecars do Stage: {Dir}", targetStageDir);
                }
            }

            // Limpa pastas pai vazias na origem
            CleanEmptyParentDirectoriesWithLog(Path.GetDirectoryName(mediaPath), monitorSources);
            LogInfo($"[1 MÍDIA POR VEZ] Mídia {mediaFileName} pronta para envio ao Telegram pelo Watcher.");
        }
    }

    private static async Task MoveOrCopyFileAsync(string sourcePath, string targetPath, CancellationToken cancellationToken)
    {
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // 1. Tenta mover atomicamente primeiro (rápido no mesmo volume ou suportado pelo SO)
        try
        {
            File.Move(sourcePath, targetPath, true);
            return;
        }
        catch (IOException)
        {
            // Se falhar por restrição cross-volume, realiza cópia em stream assíncrono com buffer de 4MB e apaga origem
        }

        var buffer = new byte[4 * 1024 * 1024];
        await using (var srcStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, true))
        await using (var dstStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true))
        {
            int read;
            while ((read = await srcStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await dstStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }

        try
        {
            if (File.Exists(sourcePath))
            {
                File.Delete(sourcePath);
            }
        }
        catch
        {
            // Ignora se não conseguir deletar imediatamente
        }
    }

    private static async Task<bool> IsFileReadyAsync(string filename, CancellationToken cancellationToken)
    {
        try
        {
            long previousLength = -1;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var inputStream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (inputStream.Length <= 0 || (inputStream.Length != previousLength && previousLength >= 0))
                    {
                        previousLength = inputStream.Length;
                    }
                    else
                    {
                        return true;
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            }

            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private async Task<bool> DownloadMultipartAsync(
        string url,
        string targetFilePath,
        int partsCount,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(targetFilePath);

        long totalSize = 0;
        bool supportsRange = false;

        try
        {
            using var headReq = new HttpRequestMessage(HttpMethod.Head, url);
            using var headResp = await _httpClient.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!headResp.IsSuccessStatusCode)
            {
                if ((int)headResp.StatusCode is 401 or 403 or 404)
                {
                    throw new HttpRequestException($"HTTP Error {(int)headResp.StatusCode}: {headResp.StatusCode}", null, headResp.StatusCode);
                }
            }
            else
            {
                totalSize = headResp.Content.Headers.ContentLength ?? 0;
                supportsRange = headResp.Headers.AcceptRanges.Contains("bytes");
            }

            if (!supportsRange)
            {
                using var rangeReq = new HttpRequestMessage(HttpMethod.Get, url);
                rangeReq.Headers.Range = new RangeHeaderValue(0, 0);
                using var rangeResp = await _httpClient.SendAsync(rangeReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!rangeResp.IsSuccessStatusCode && (int)rangeResp.StatusCode is 401 or 403 or 404)
                {
                    throw new HttpRequestException($"HTTP Error {(int)rangeResp.StatusCode}: {rangeResp.StatusCode}", null, rangeResp.StatusCode);
                }

                if (TryGetRangeProbeLength(rangeResp, out var rangeLength))
                {
                    totalSize = rangeLength;
                    supportsRange = true;
                }
            }
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch
        {
            // Probe fallback via Range 0-0
            try
            {
                using var rangeReq = new HttpRequestMessage(HttpMethod.Get, url);
                rangeReq.Headers.Range = new RangeHeaderValue(0, 0);
                using var rangeResp = await _httpClient.SendAsync(rangeReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!rangeResp.IsSuccessStatusCode && (int)rangeResp.StatusCode is 401 or 403 or 404)
                {
                    throw new HttpRequestException($"HTTP Error {(int)rangeResp.StatusCode}: {rangeResp.StatusCode}", null, rangeResp.StatusCode);
                }

                if (TryGetRangeProbeLength(rangeResp, out var rangeLength))
                {
                    totalSize = rangeLength;
                    supportsRange = true;
                }
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch
            {
                // Download direto
            }
        }

        if (totalSize > 0)
        {
            var targetDriveRoot = Path.GetPathRoot(Path.GetFullPath(targetFilePath));
            if (!string.IsNullOrWhiteSpace(targetDriveRoot))
            {
                try
                {
                    var drive = new DriveInfo(targetDriveRoot);
                    if (drive.IsReady && drive.AvailableFreeSpace < (totalSize + 250L * 1024 * 1024))
                    {
                        throw new IOException(
                            $"Espaço insuficiente no disco: '{targetDriveRoot}' possui apenas {FormatBytes(drive.AvailableFreeSpace)} livres, mas o arquivo requer {FormatBytes(totalSize)}.");
                    }
                }
                catch (IOException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[NEBULA-DOWNLOADER] Não foi possível verificar espaço livre no drive {Drive}", targetDriveRoot);
                }
            }
        }

        var status = new NebulaDownloadStatusDto
        {
            Name = fileName,
            TotalMb = Math.Round(totalSize / (1024.0 * 1024.0), 2),
            DoneMb = 0,
            Percentage = 0,
            StageStep = "Baixando mídia...",
            DetailText = "0%"
        };
        OnProgressChanged?.Invoke(status);

        var tempOutputFile = targetFilePath + ".download.tmp";
        var partFilesToClean = Array.Empty<string>();

        try
        {
            if (!supportsRange || totalSize <= 0 || partsCount <= 1)
            {
                // Download sequencial
                using var resp = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"HTTP Error {(int)resp.StatusCode}: {resp.StatusCode}", null, resp.StatusCode);
                }

                using var contentStream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var fileStream = new FileStream(tempOutputFile, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true);

                var buffer = new byte[128 * 1024];
                long totalRead = 0;
                int read;
                var startTime = DateTime.UtcNow;
                var nextPercent = 1;
                long lastLoggedBytes = 0;

                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readCts.CancelAfter(TimeSpan.FromSeconds(45));

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, readCts.Token).ConfigureAwait(false)) > 0)
                {
                    readCts.CancelAfter(TimeSpan.FromSeconds(45));
                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    totalRead += read;

                    var elapsed = Math.Max(0.1, (DateTime.UtcNow - startTime).TotalSeconds);
                    var speedMb = (totalRead / (1024.0 * 1024.0)) / elapsed;
                    var percent = totalSize > 0 ? (totalRead * 100.0) / totalSize : 50;

                    status.DoneMb = Math.Round(totalRead / (1024.0 * 1024.0), 2);
                    status.Percentage = Math.Round(percent, 1);
                    status.Speed = $"{speedMb:F1} MB/s";
                    status.DetailText = $"{status.Percentage:F1}% ({FormatBytes(totalRead)}) - {status.Speed}";
                    OnProgressChanged?.Invoke(status);

                    if (percent >= nextPercent || (totalRead - lastLoggedBytes) >= 25 * 1024 * 1024)
                    {
                        LogInfo($"Baixando {fileName}: {status.DoneMb:F1} MB / {status.TotalMb:F1} MB ({(int)percent}%) - Vel: {speedMb:F1} MB/s");
                        nextPercent = (int)percent + 1;
                        lastLoggedBytes = totalRead;
                    }
                }

                if (totalSize > 0 && totalRead != totalSize)
                {
                    throw new IOException($"Download sequencial incompleto: {totalRead} de {totalSize} bytes.");
                }
            }
            else
            {
                // Download multipart com suporte a Range
                var partSize = (totalSize + partsCount - 1) / partsCount;
                var partFiles = new string[partsCount];
                partFilesToClean = partFiles;
                for (int i = 0; i < partsCount; i++)
                {
                    partFiles[i] = $"{targetFilePath}.part{i}";
                }

                var partDownloadedAmounts = new long[partsCount];
                var progressLock = new object();
                var startTime = DateTime.UtcNow;
                var nextPercent = 1;
                long lastLoggedBytes = 0;

                // Checa partes existentes para Resume
                for (int i = 0; i < partsCount; i++)
                {
                    var start = i * partSize;
                    var end = Math.Min(start + partSize, totalSize) - 1;
                    var expected = end - start + 1;

                    if (File.Exists(partFiles[i]))
                    {
                        var len = new FileInfo(partFiles[i]).Length;
                        if (len == expected)
                        {
                            partDownloadedAmounts[i] = expected;
                        }
                        else
                        {
                            File.Delete(partFiles[i]);
                        }
                    }
                }

                long initialDownloaded = partDownloadedAmounts.Sum();
                if (initialDownloaded > 0)
                {
                    var resPct = (int)((initialDownloaded * 100.0) / totalSize);
                    LogInfo($"Resumindo {fileName}: {(initialDownloaded / (1024.0 * 1024.0)):F1} MB / {status.TotalMb:F1} MB ({resPct}%) já no disco.");
                    nextPercent = resPct + 1;
                    lastLoggedBytes = initialDownloaded;
                }

                // Use todas as partes configuradas (até 32), como no downloader
                // original. O limite anterior de 8 deixava o MulletaFlix bem
                // mais lento quando DownloadParts era 20 ou 32.
                var connectionCount = Math.Clamp(partsCount, 1, 32);
                using var semaphore = new SemaphoreSlim(connectionCount, connectionCount);

                var downloadTasks = Enumerable.Range(0, partsCount).Select(async index =>
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var start = index * partSize;
                        var end = Math.Min(start + partSize, totalSize) - 1;
                        var expected = end - start + 1;
                        var partFile = partFiles[index];

                        if (File.Exists(partFile) && new FileInfo(partFile).Length == expected)
                        {
                            return; // Já concluído
                        }

                        var partTmp = partFile + ".tmp";
                        const int maxPartAttempts = 4;

                        for (int attempt = 1; attempt <= maxPartAttempts; attempt++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (File.Exists(partTmp))
                            {
                                File.Delete(partTmp);
                            }

                            lock (progressLock)
                            {
                                partDownloadedAmounts[index] = 0;
                            }

                            try
                            {
                                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                                req.Headers.Range = new RangeHeaderValue(start, end);

                                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                                ValidateRangeResponse(resp, start, end, totalSize, expected);
                                using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                                using var fs = new FileStream(partTmp, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, true);

                                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                                readCts.CancelAfter(TimeSpan.FromSeconds(45));

                                var buf = new byte[64 * 1024];
                                int r;
                                long partBytesRead = 0;
                                while ((r = await stream.ReadAsync(buf, 0, buf.Length, readCts.Token).ConfigureAwait(false)) > 0)
                                {
                                    readCts.CancelAfter(TimeSpan.FromSeconds(45));
                                    await fs.WriteAsync(buf, 0, r, cancellationToken).ConfigureAwait(false);
                                    partBytesRead += r;
                                    lock (progressLock)
                                    {
                                        partDownloadedAmounts[index] = partBytesRead;
                                        var currentDownloaded = partDownloadedAmounts.Sum();
                                        var elapsed = Math.Max(0.1, (DateTime.UtcNow - startTime).TotalSeconds);
                                        var speedMb = (currentDownloaded / (1024.0 * 1024.0)) / elapsed;
                                        var percent = (currentDownloaded * 100.0) / totalSize;

                                        status.DoneMb = Math.Round(currentDownloaded / (1024.0 * 1024.0), 2);
                                        status.Percentage = Math.Round(percent, 1);
                                        status.Speed = $"{speedMb:F1} MB/s";
                                        status.DetailText = $"{status.Percentage:F1}% ({FormatBytes(currentDownloaded)}/{FormatBytes(totalSize)}) - {status.Speed}";
                                        OnProgressChanged?.Invoke(status);

                                        if (percent >= nextPercent || (currentDownloaded - lastLoggedBytes) >= 25 * 1024 * 1024)
                                        {
                                            LogInfo($"Baixando {fileName}: {status.DoneMb:F1} MB / {status.TotalMb:F1} MB ({(int)percent}%) - Vel: {speedMb:F1} MB/s");
                                            nextPercent = (int)percent + 1;
                                            lastLoggedBytes = currentDownloaded;
                                        }
                                    }
                                }

                                fs.Close();
                                if (partBytesRead != expected)
                                {
                                    throw new IOException($"Parte {index} incompleta: {partBytesRead} de {expected} bytes.");
                                }

                                if (File.Exists(partFile))
                                {
                                    File.Delete(partFile);
                                }

                                File.Move(partTmp, partFile);
                                break; // Sucesso na parte!
                            }
                            catch (Exception ex) when (attempt < maxPartAttempts && !cancellationToken.IsCancellationRequested)
                            {
                                lock (progressLock)
                                {
                                    partDownloadedAmounts[index] = 0;
                                }
                                var errDesc = ex is OperationCanceledException ? "conexão estagnou (sem dados por 45s)" : ex.Message;
                                LogInfo($"[STRM] Parte {index + 1}/{partsCount} de {fileName} {errDesc} (tentativa {attempt}/{maxPartAttempts}). Reconectando...");
                                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(downloadTasks).ConfigureAwait(false);

                // Junta todas as partes no arquivo final
                using (var outputStream = new FileStream(tempOutputFile, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    for (int i = 0; i < partsCount; i++)
                    {
                        using (var partStream = new FileStream(partFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                        {
                            await partStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                var assembledLength = new FileInfo(tempOutputFile).Length;
                if (assembledLength != totalSize)
                {
                    throw new IOException($"Arquivo unido incompleto: {assembledLength} de {totalSize} bytes.");
                }
            }

            File.Move(tempOutputFile, targetFilePath, overwrite: true);

            // Só remove as partes depois que o arquivo final foi movido com
            // sucesso. Assim uma falha de união/movimentação ainda permite resume.
            foreach (var partFile in partFilesToClean)
            {
                DeleteIfExists(partFile);
            }

            status.Percentage = 100;
            status.StageStep = "Concluído";
            status.DetailText = "100% Concluído";
            OnProgressChanged?.Invoke(status);

            return true;
        }
        catch
        {
            // Preserve .part files for resume capability - only clean up .tmp files and temp output
            DeleteIfExists(tempOutputFile);
            foreach (var partFile in partFilesToClean)
            {
                // Only delete .tmp files, keep .part files for resume
                DeleteIfExists(partFile + ".tmp");
            }

            throw;
        }
    }

    internal static void ValidateRangeResponse(HttpResponseMessage response, long expectedStart, long expectedEnd, long totalSize, long expectedLength)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP Error {(int)response.StatusCode}: {response.StatusCode}", null, response.StatusCode);
        }

        if ((int)response.StatusCode != 206)
        {
            throw new IOException($"Servidor ignorou Range: HTTP {(int)response.StatusCode}.");
        }

        var range = response.Content.Headers.ContentRange;
        if (range == null ||
            range.From != expectedStart ||
            range.To != expectedEnd ||
            range.Length != totalSize)
        {
            throw new IOException("Content-Range incompatível com a parte solicitada.");
        }

        if (response.Content.Headers.ContentLength.HasValue &&
            response.Content.Headers.ContentLength.Value != expectedLength)
        {
            throw new IOException("Content-Length incompatível com a parte solicitada.");
        }
    }

    internal static bool TryGetRangeProbeLength(HttpResponseMessage response, out long totalSize)
    {
        totalSize = 0;
        var range = response.Content.Headers.ContentRange;
        if (response.StatusCode != System.Net.HttpStatusCode.PartialContent ||
            range == null ||
            range.From != 0 ||
            range.To != 0 ||
            !range.Length.HasValue)
        {
            return false;
        }

        if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != 1)
        {
            return false;
        }

        totalSize = range.Length.Value;
        return totalSize > 0;
    }

    private static void DeleteIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private async Task EnqueueFileInMongoAsync(string filePath, string relDir, CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(filePath);
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            return;
        }

        var (parentId, _) = await EnsureDirectoryStructureInMongoAsync(relDir, cancellationToken).ConfigureAwait(false);

        var existing = await _mongoContext.FindByNameAndParentAsync(fileName, parentId, cancellationToken).ConfigureAwait(false);
        if (existing == null)
        {
            var nodeDoc = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "name", fileName },
                { "type", "file" },
                { "is_directory", false },
                { "status", "queued" },
                { "size", fileInfo.Length },
                { "local_path", filePath },
                { "parent", string.IsNullOrEmpty(parentId) ? BsonNull.Value : (ObjectId.TryParse(parentId, out var pOid) ? (BsonValue)pOid : parentId) },
                { "delete_source", true },
                { "parts", new BsonArray() },
                { "created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { "modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
            };

            await _mongoContext.InsertFileDocAsync(nodeDoc, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("[NEBULA-FEEDER] Mídia {Name} enfileirada no MongoDB com sucesso.", fileName);
        }
    }

    private async Task<(string? ParentId, string VirtualPath)> EnsureDirectoryStructureInMongoAsync(string relDir, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relDir))
        {
            return (null, string.Empty);
        }

        var rawParts = relDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var parts = rawParts.Where((p, idx) => !(idx == 0 && string.Equals(p, "strm", StringComparison.OrdinalIgnoreCase))).ToArray();

        if (parts.Length == 0)
        {
            return (null, string.Empty);
        }

        string? currentParent = null;
        var currentPath = string.Empty;

        foreach (var part in parts)
        {
            var parentVirtualPath = string.IsNullOrEmpty(currentPath) ? "/" : $"/{currentPath}";
            var existing = await _mongoContext.FindByNameAndParentAsync(part, currentParent, parentVirtualPath, cancellationToken).ConfigureAwait(false);
            if (existing != null)
            {
                currentParent = existing.GetValue("_id").ToString();
            }
            else
            {
                var dirId = ObjectId.GenerateNewId();
                var dirDoc = new BsonDocument
                {
                    { "_id", dirId },
                    { "name", part },
                    { "type", "dir" },
                    { "is_directory", true },
                    { "status", "completed" },
                    { "parent", string.IsNullOrEmpty(currentParent) ? BsonNull.Value : (ObjectId.TryParse(currentParent, out var cpOid) ? (BsonValue)cpOid : currentParent) },
                    { "created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    { "modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                };

                await _mongoContext.InsertFileDocAsync(dirDoc, cancellationToken).ConfigureAwait(false);
                currentParent = dirId.ToString();
            }

            currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
        }

        return (currentParent, currentPath);
    }

    private async Task<(bool IsDuplicate, bool IsCompleted, string Reason)> CheckMediaDuplicateInMongoAsync(
        string strmPath,
        string finalMediaFileName,
        string url,
        CancellationToken cancellationToken)
    {
        // 0. Checa se já existe mídia concluída no Telegram (verificação universal via FindCompletedMediaAsync)
        var completedDoc = await _mongoContext.FindCompletedMediaAsync(finalMediaFileName, strmPath, cancellationToken).ConfigureAwait(false);
        if (completedDoc != null)
        {
            var matchedName = completedDoc.GetValue("name", finalMediaFileName).AsString;
            return (true, true, $"Mídia '{matchedName}' já concluída e enviada ao Telegram anteriormente");
        }

        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(strmPath);
        var files = await _mongoContext.GetCompletedOrActiveFilesAsync(cancellationToken).ConfigureAwait(false);

        // 1. Checa por ID na URL do STRM (ex: ?id=64f... ou /stream?id=... ou /12345.mkv)
        if (!string.IsNullOrWhiteSpace(url))
        {
            var match = Regex.Match(url, @"[?&]id=([a-fA-F0-9]{24})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var idStr = match.Groups[1].Value;
                var matchingDoc = files.FirstOrDefault(d => string.Equals(d.GetValue("_id", string.Empty).ToString(), idStr, StringComparison.OrdinalIgnoreCase));
                if (matchingDoc != null)
                {
                    var matchedName = matchingDoc.GetValue("name", string.Empty).AsString;
                    var isCompleted = string.Equals(matchingDoc.GetValue("status", string.Empty).AsString, "completed", StringComparison.OrdinalIgnoreCase);
                    return (true, isCompleted, $"Mídia '{matchedName}' com ID ({idStr}) já encontrada");
                }
            }
        }

        // 2. Checa por Nome, Título ou URL idêntica em mídias no acervo
        var normFileName = NormalizeMediaTitle(fileNameWithoutExt);
        var movieIdent = MovieIdentity(fileNameWithoutExt) ?? MovieIdentity(Path.GetFileName(Path.GetDirectoryName(strmPath) ?? string.Empty));
        var epIdent = EpisodeIdentity(Path.GetFileName(Path.GetDirectoryName(strmPath) ?? string.Empty), strmPath);

        foreach (var doc in files)
        {
            var name = doc.GetValue("name", string.Empty).AsString;
            var docStem = Path.GetFileNameWithoutExtension(name);
            var status = doc.GetValue("status", string.Empty).AsString;

            var docUrl = doc.Contains("source_url") ? doc["source_url"].AsString :
                         doc.Contains("url") ? doc["url"].AsString :
                         doc.Contains("strm_url") ? doc["strm_url"].AsString : string.Empty;

            if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(docUrl) &&
                string.Equals(url.Trim(), docUrl.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                var isCompleted = string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);
                return (true, isCompleted, $"Mídia '{name}' possui link idêntico já encontrado");
            }

            if (string.Equals(name, finalMediaFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(docStem, fileNameWithoutExt, StringComparison.OrdinalIgnoreCase))
            {
                if (status is "staging" or "queued" or "uploading")
                {
                    return (true, false, $"Mídia '{fileNameWithoutExt}' já está ativa/em fila no Nebula");
                }

                return (true, true, $"Mídia '{name}' com título idêntico já concluída/enviada");
            }

            // Identidade de Filme
            if (movieIdent.HasValue)
            {
                var docMovieIdent = MovieIdentity(docStem);
                if (docMovieIdent.HasValue &&
                    string.Equals(movieIdent.Value.Title, docMovieIdent.Value.Title, StringComparison.OrdinalIgnoreCase) &&
                    movieIdent.Value.Year == docMovieIdent.Value.Year)
                {
                    if (status is "staging" or "queued" or "uploading")
                    {
                        return (true, false, $"Mídia '{fileNameWithoutExt}' já está ativa/em fila no Nebula");
                    }

                    return (true, true, $"Filme '{name}' ({movieIdent.Value.Year}) já concluído no Nebula");
                }
            }

            // Identidade de Série
            if (epIdent.HasValue)
            {
                var docEpIdent = EpisodeIdentity(string.Empty, name);
                if (docEpIdent.HasValue &&
                    string.Equals(epIdent.Value.Series, docEpIdent.Value.Series, StringComparison.OrdinalIgnoreCase) &&
                    epIdent.Value.Season == docEpIdent.Value.Season &&
                    epIdent.Value.Episode == docEpIdent.Value.Episode)
                {
                    var isCompleted = string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase);
                    return (true, isCompleted, $"Episódio '{epIdent.Value.Series} S{epIdent.Value.Season:02d}E{epIdent.Value.Episode:02d}' já encontrado no Nebula");
                }
            }
        }

        return (false, false, string.Empty);
    }

    internal static string NormalizeMediaTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalizedString = value.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder();
        foreach (var c in normalizedString)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        var cleaned = Regex.Replace(sb.ToString().ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
        return Regex.Replace(cleaned, @"\s+", " ");
    }

    internal static (string Title, int Year)? MovieIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var years = YearRegex.Matches(value);
        if (years.Count == 0)
        {
            return null;
        }

        var lastYearMatch = years[^1];
        var year = int.Parse(lastYearMatch.Value, CultureInfo.InvariantCulture);
        var titlePart = value[..lastYearMatch.Index];
        var normTitle = NormalizeMediaTitle(titlePart);
        return string.IsNullOrEmpty(normTitle) ? null : (normTitle, year);
    }

    internal static (string Series, int Season, int Episode)? EpisodeIdentity(string seriesName, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var m = EpisodeRegex.Match(stem);
        if (!m.Success)
        {
            return null;
        }

        var prefix = m.Groups["prefix"].Value.Trim(' ', '.', '_', '-');
        var effective = !string.IsNullOrEmpty(seriesName) && !seriesName.Contains("season", StringComparison.OrdinalIgnoreCase)
            ? seriesName : prefix;
        if (string.IsNullOrEmpty(effective))
        {
            effective = prefix;
        }

        var normSeries = NormalizeMediaTitle(effective);
        if (string.IsNullOrEmpty(normSeries))
        {
            return null;
        }

        var season = int.Parse(m.Groups["season"].Value, CultureInfo.InvariantCulture);
        var episode = int.Parse(m.Groups["episode"].Value, CultureInfo.InvariantCulture);
        return (normSeries, season, episode);
    }

    private static async Task<string> ReadStrmUrlAsync(string strmPath, CancellationToken cancellationToken)
    {
        var lines = await File.ReadAllLinesAsync(strmPath, cancellationToken).ConfigureAwait(false);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith('#'))
            {
                return trimmed;
            }
        }

        return string.Empty;
    }

    private static string GuessExtensionFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (VideoExtensions.Contains(ext))
            {
                return ext;
            }
        }
        catch
        {
            // Ignora
        }

        return ".mp4";
    }

    private static int ExtractMediaYear(string name, string parentName)
    {
        foreach (var text in new[] { name, parentName })
        {
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            var matches = YearRegex.Matches(text);
            if (matches.Count > 0 && int.TryParse(matches[^1].Value, out var y))
            {
                return y;
            }
        }

        return 0;
    }

    private static int GetCategoryPriority(string path)
    {
        var mediaType = NebulaUploadEngine.ClassifyMediaType(
            Path.GetDirectoryName(path),
            Path.GetFileName(path));

        return mediaType switch
        {
            "FILME" => 1,
            "PORNO" => 2,
            "SERIE" => 3,
            _ => 4
        };
    }

    private static string GetCategoryDisplayName(int categoryPriority) => categoryPriority switch
    {
        1 => "FILMES",
        2 => "PORNO",
        3 => "SERIES",
        _ => "OUTROS"
    };

    /// <summary>
    /// Exclui arquivos sidecars (.nfo, .xml, .srt, imagens, etc.) associados a uma mídia que já foi concluída no Telegram.
    /// </summary>
    private void DeleteAssociatedSidecars(string mediaPath, string stem)
    {
        var dir = Path.GetDirectoryName(mediaPath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            return;
        }

        try
        {
            var otherMediaFiles = Directory.EnumerateFiles(dir)
                .Where(f => !string.Equals(f, mediaPath, StringComparison.OrdinalIgnoreCase) &&
                            SupportedMediaExtensions.Contains(Path.GetExtension(f)))
                .ToList();

            foreach (var file in Directory.EnumerateFiles(dir))
            {
                var ext = Path.GetExtension(file);
                if (!SupportedSidecarExtensions.Contains(ext) && !string.Equals(ext, ".strm", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var sidecarStem = Path.GetFileNameWithoutExtension(file);
                if (string.Equals(sidecarStem, stem, StringComparison.OrdinalIgnoreCase) || otherMediaFiles.Count == 0)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                            LogInfo($"Sidecar de mídia já concluída no Telegram removido: {Path.GetFileName(file)}");
                            _logger.LogInformation("[NEBULA-DOWNLOADER] Sidecar de mídia já concluída removido: {File}", file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[NEBULA-DOWNLOADER] Erro ao deletar sidecar de mídia concluída: {Path}", file);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-DOWNLOADER] Erro ao limpar sidecars em {Dir}", dir);
        }
    }

    private void DeleteTargetStageDirectoryIfCompleted(string targetStageDir)
    {
        if (string.IsNullOrWhiteSpace(targetStageDir) || !Directory.Exists(targetStageDir))
        {
            return;
        }

        try
        {
            var stageFiles = Directory.EnumerateFiles(targetStageDir, "*", SearchOption.AllDirectories).ToList();
            var hasActiveMedia = stageFiles.Any(f => SupportedMediaExtensions.Contains(Path.GetExtension(f)));
            if (!hasActiveMedia)
            {
                foreach (var file in stageFiles)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                            _logger.LogInformation("[NEBULA-DOWNLOADER] Arquivo em staging de mídia já concluída removido: {File}", file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[NEBULA-DOWNLOADER] Não foi possível remover arquivo do stage {File}", file);
                    }
                }

                if (!Directory.EnumerateFileSystemEntries(targetStageDir).Any())
                {
                    Directory.Delete(targetStageDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-DOWNLOADER] Erro ao verificar diretório de stage {Dir}", targetStageDir);
        }
    }

    /// <summary>
    /// Limpa recursivamente diretórios pais vazios a partir de uma pasta até atingir a raiz monitorada.
    /// </summary>
    private void CleanEmptyParentDirectoriesWithLog(string? dirPath, List<string> monitorRoots)
    {
        if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
        {
            return;
        }

        var currentDir = dirPath;
        while (!string.IsNullOrWhiteSpace(currentDir) && Directory.Exists(currentDir))
        {
            var isRoot = monitorRoots.Any(root => string.Equals(
                Path.GetFullPath(currentDir).TrimEnd('\\', '/'),
                Path.GetFullPath(root).TrimEnd('\\', '/'),
                StringComparison.OrdinalIgnoreCase));

            if (isRoot || Path.GetPathRoot(currentDir)?.TrimEnd('\\', '/') == currentDir.TrimEnd('\\', '/'))
            {
                break;
            }

            try
            {
                var hasFiles = Directory.EnumerateFiles(currentDir, "*", SearchOption.AllDirectories).Any();

                if (!hasFiles)
                {
                    Directory.Delete(currentDir, true);
                    LogInfo($"Pasta vazia removida: {currentDir}");
                    _logger.LogInformation("[NEBULA-DOWNLOADER] Pasta vazia removida: {Dir}", currentDir);
                    currentDir = Path.GetDirectoryName(currentDir);
                }
                else
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NEBULA-DOWNLOADER] Não foi possível verificar/remover diretório vazio {Dir}", currentDir);
                break;
            }
        }
    }

    /// <summary>
    /// Varre e remove recursivamente todas as subpastas vazias dentro de uma raiz de monitoramento.
    /// </summary>
    private void CleanAllEmptySubdirectories(string rootDir)
    {
        if (!Directory.Exists(rootDir))
        {
            return;
        }

        try
        {
            foreach (var subDir in Directory.GetDirectories(rootDir))
            {
                CleanDirectoryRecursive(subDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-DOWNLOADER] Falha na limpeza de pastas vazias em {Dir}", rootDir);
        }
    }

    private void CleanDirectoryRecursive(string dir)
    {
        try
        {
            foreach (var sub in Directory.GetDirectories(dir))
            {
                CleanDirectoryRecursive(sub);
            }

            if (Directory.Exists(dir))
            {
                var hasFiles = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any();
                if (!hasFiles)
                {
                    Directory.Delete(dir, true);
                    LogInfo($"Pasta vazia removida: {dir}");
                    _logger.LogInformation("[NEBULA-DOWNLOADER] Pasta vazia removida: {Dir}", dir);
                }
            }
        }
        catch
        {
            // Ignora
        }
    }

    private static string GetRelativePathFromSource(string fullPath, List<string> sources)
    {
        var dir = Path.GetDirectoryName(fullPath) ?? string.Empty;
        foreach (var src in sources)
        {
            if (IsPathWithinRoot(dir, src))
            {
                var normalizedDir = Path.GetFullPath(dir).TrimEnd('\\', '/');
                var normalizedSource = Path.GetFullPath(src).TrimEnd('\\', '/');
                var rel = normalizedDir[normalizedSource.Length..].TrimStart('\\', '/');
                var parts = rel.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                var filtered = parts.Where((p, idx) => !(idx == 0 && string.Equals(p, "strm", StringComparison.OrdinalIgnoreCase)));
                return string.Join(Path.DirectorySeparatorChar, filtered);
            }
        }

        return string.Empty;
    }

    internal static bool IsPathWithinRoot(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public const double MinimumFreeSpacePercentThreshold = 10.0;
    public const long MinimumFreeBytesThreshold = 5L * 1024 * 1024 * 1024; // 5 GB

    internal static string SelectBestStageDirectory(
        IReadOnlyList<string> stageRoots,
        Action<string>? logInfo = null,
        Action<string>? logWarning = null,
        ILogger? logger = null,
        long requiredBytes = 0,
        Func<string, (bool IsReady, long FreeBytes, long TotalBytes)>? driveInspector = null)
    {
        if (stageRoots == null || stageRoots.Count == 0)
        {
            var defaultFallback = Path.Combine(AppContext.BaseDirectory, "NebulaStage");
            try { Directory.CreateDirectory(defaultFallback); } catch { }
            return defaultFallback;
        }

        driveInspector ??= root =>
        {
            var fullPath = Path.GetFullPath(root);
            var driveRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(driveRoot))
            {
                return (false, 0, 0);
            }

            var drive = new DriveInfo(driveRoot);
            return drive.IsReady ? (true, drive.AvailableFreeSpace, drive.TotalSize) : (false, 0, 0);
        };

        var candidates = new List<(string Root, string DriveRoot, long FreeBytes, long TotalBytes, double FreePercent)>();

        foreach (var root in stageRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(root);
                var fullPath = Path.GetFullPath(root);
                var driveRoot = Path.GetPathRoot(fullPath) ?? string.Empty;
                var (isReady, free, total) = driveInspector(root);
                if (isReady && total > 0)
                {
                    var pct = ((double)free / total) * 100.0;
                    candidates.Add((root, driveRoot, free, total, pct));
                }
                else if (isReady)
                {
                    candidates.Add((root, driveRoot, free, total, 0.0));
                }
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "[NEBULA-STAGE] Erro ao obter informações de volume para o stage root {Root}", root);
            }
        }

        if (candidates.Count == 0)
        {
            var fallback = stageRoots[0];
            try { Directory.CreateDirectory(fallback); } catch { }
            return fallback;
        }

        // Regra dos 10%: Um disco é elegível se tiver >= 10% de espaço livre E pelo menos 5 GB (ou requiredBytes se maior)
        var minBytes = Math.Max(MinimumFreeBytesThreshold, requiredBytes);
        var eligible = candidates
            .Where(c => c.FreePercent >= MinimumFreeSpacePercentThreshold && c.FreeBytes >= minBytes)
            .ToList();

        if (eligible.Count > 0)
        {
            var chosen = eligible[0];
            if (candidates[0].Root != chosen.Root)
            {
                var first = candidates[0];
                var msg = $"Disco {first.DriveRoot} com espaço livre abaixo de 10% ({first.FreePercent:F1}% livre, {FormatBytes(first.FreeBytes)}). Alternando automaticamente para {chosen.DriveRoot} ({chosen.FreePercent:F1}% livre, {FormatBytes(chosen.FreeBytes)}).";
                logInfo?.Invoke(msg);
                logger?.LogInformation("[NEBULA-STAGE] {Message}", msg);
            }

            return chosen.Root;
        }

        // Se nenhum disco possui >= 10% e >= 5 GB livres, seleciona o que possui a maior quantidade de espaço disponível
        var bestBySpace = candidates.OrderByDescending(c => c.FreeBytes).First();
        var warnMsg = $"AVISO: Todos os discos de stage estão abaixo de 10% de espaço livre! Usando disco com maior espaço disponível: {bestBySpace.DriveRoot} ({bestBySpace.FreePercent:F1}% livre, {FormatBytes(bestBySpace.FreeBytes)}).";
        logWarning?.Invoke(warnMsg);
        logger?.LogWarning("[NEBULA-STAGE] {Message}", warnMsg);

        return bestBySpace.Root;
    }

    private string SelectBestStageDirectory(List<string> stageRoots, long requiredBytes = 0)
        => SelectBestStageDirectory(stageRoots, LogInfo, LogWarning, _logger, requiredBytes);

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }

        return $"{number:n1} {suffixes[counter]}";
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _httpClient.Dispose();
        _cts.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class FailureTracker
    {
        private readonly ConcurrentDictionary<string, (DateTime Timestamp, int Count, string Error)> _failures = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _cooldown = TimeSpan.FromHours(2);

        public bool ShouldSkip(string filePath, out string reason)
        {
            reason = string.Empty;
            if (_failures.TryGetValue(filePath, out var info))
            {
                var isFatal = info.Error.Contains("401", StringComparison.OrdinalIgnoreCase) ||
                              info.Error.Contains("403", StringComparison.OrdinalIgnoreCase) ||
                              info.Error.Contains("404", StringComparison.OrdinalIgnoreCase) ||
                              info.Error.Contains("410", StringComparison.OrdinalIgnoreCase);
                var effectiveCooldown = isFatal ? _cooldown * 2 : _cooldown;
                if (DateTime.UtcNow - info.Timestamp < effectiveCooldown)
                {
                    reason = $"{info.Error} (Tentativas: {info.Count}, Cooldown ativo)";
                    return true;
                }
            }

            return false;
        }

        public void RecordFailure(string filePath, string error)
        {
            _failures.AddOrUpdate(
                filePath,
                _ => (DateTime.UtcNow, 1, error),
                (_, old) => (DateTime.UtcNow, old.Count + 1, error));
        }

        public void RecordSuccess(string filePath)
        {
            _failures.TryRemove(filePath, out _);
        }

        public void Clear()
        {
            _failures.Clear();
        }
    }
}
