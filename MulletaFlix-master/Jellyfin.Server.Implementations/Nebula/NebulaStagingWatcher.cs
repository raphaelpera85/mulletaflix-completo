using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Monitor de diretórios de staging e orquestrador de fila de uploads paralelos para o Telegram.
/// </summary>
public sealed class NebulaStagingWatcher : IAsyncDisposable, IDisposable
{
    private readonly ILogger<NebulaStagingWatcher> _logger;
    private readonly NebulaUploadEngine _uploadEngine;
    private readonly NebulaMongoContext? _mongoContext;
    private readonly Action<string, string>? _emitServerLog;
    private readonly int _botCount;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly List<string> _stagingDirs = [];
    private readonly ConcurrentQueue<NebulaUploadTaskItem> _pendingFiles = new();
    private readonly ConcurrentDictionary<string, byte> _queuedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, byte> _activeFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _workerTasks = [];
    private Task? _masterTask;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaStagingWatcher"/>.
    /// </summary>
    /// <param name="uploadEngine">Motor de upload para o Telegram.</param>
    /// <param name="mongoContext">Contexto do MongoDB.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="emitServerLog">Callback de logs formatados do servidor.</param>
    /// <param name="botCount">Quantidade de bots disponíveis.</param>
    public NebulaStagingWatcher(
        NebulaUploadEngine uploadEngine,
        NebulaMongoContext? mongoContext,
        ILogger<NebulaStagingWatcher> logger,
        Action<string, string>? emitServerLog = null,
        int botCount = 27)
    {
        _uploadEngine = uploadEngine;
        _mongoContext = mongoContext;
        _logger = logger;
        _emitServerLog = emitServerLog;
        _botCount = botCount > 0 ? botCount : 27;
    }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaStagingWatcher"/> sem contexto Mongo.
    /// </summary>
    /// <param name="uploadEngine">Motor de upload para o Telegram.</param>
    /// <param name="logger">Logger.</param>
    public NebulaStagingWatcher(NebulaUploadEngine uploadEngine, ILogger<NebulaStagingWatcher> logger)
        : this(uploadEngine, null, logger)
    {
    }

    private void EmitServer(string level, string message)
    {
        _logger.LogInformation("[NEBULA-WATCHER] {Message}", message);
        _emitServerLog?.Invoke(level, message);
    }

    /// <summary>
    /// Inicia o monitoramento dos diretórios informados e a recuperação da fila pendente do MongoDB.
    /// </summary>
    /// <param name="stagingDirs">Diretórios de staging monitorados.</param>
    public void Start(IEnumerable<string> stagingDirs)
    {
        Start(stagingDirs, 4);
    }

    /// <summary>
    /// Inicia o monitoramento dos diretórios informados com contagem de workers especificada.
    /// </summary>
    /// <param name="stagingDirs">Diretórios de staging monitorados.</param>
    /// <param name="workerCount">Quantidade de workers simultâneos.</param>
    public void Start(IEnumerable<string> stagingDirs, int workerCount)
    {
        if (_isRunning)
        {
            return;
        }

        _stagingDirs.Clear();
        foreach (var dir in stagingDirs)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var watcher = new FileSystemWatcher(dir)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };

                watcher.Created += (s, e) => EnqueueFile(e.FullPath);
                watcher.Changed += (s, e) => EnqueueFile(e.FullPath);
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
                _stagingDirs.Add(dir);

                _logger.LogInformation("[NEBULA-WATCHER] Monitorando diretório de staging: {Dir}", dir);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEBULA-WATCHER] Não foi possível monitorar diretório: {Dir}", dir);
            }
        }

        _isRunning = true;
        var actualWorkers = Math.Clamp(workerCount, 1, 16);
        _masterTask = Task.Run(() => RunMasterLoopAsync(actualWorkers, _cts.Token));
    }

    /// <summary>
    /// Enfileira imediatamente uma mídia que o downloader já registrou no MongoDB.
    /// </summary>
    public void EnqueueMediaFromDownloader(string filePath)
    {
        EnqueueFile(filePath);
    }

    private void EnqueueFile(string fullPath, ObjectId? nodeId = null, string? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(fullPath) ||
            string.Equals(Path.GetFileName(fullPath), NebulaMetadataExportService.PendingMarkerFileName, StringComparison.OrdinalIgnoreCase) ||
            fullPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            fullPath.EndsWith(".download", StringComparison.OrdinalIgnoreCase) ||
            fullPath.EndsWith(".strm", StringComparison.OrdinalIgnoreCase) ||
            fullPath.Contains(".part", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // FileSystemWatcher also reports unrelated files created in the stage.
        // Keep its event path aligned with the Mongo scanner and never enqueue
        // a source STRM or an arbitrary temporary/auxiliary file.
        if (!NebulaMetadataExportService.IsUploadablePath(fullPath))
        {
            return;
        }

        var fileName = Path.GetFileName(fullPath);
        var queueKey = Path.GetFullPath(fullPath);
        if (_queuedFiles.TryAdd(queueKey, 0))
        {
            _pendingFiles.Enqueue(new NebulaUploadTaskItem
            {
                FilePath = fullPath,
                FileName = fileName,
                ParentId = parentId,
                NodeId = nodeId
            });
        }
    }

    private static bool IsMetadataSidecar(string path)
        => NebulaMetadataExportService.IsMetadataSidecarPath(path);

    private static bool HasPendingMetadataMarker(string path)
    {
        var directory = Path.GetDirectoryName(path);
        return !string.IsNullOrWhiteSpace(directory)
            && File.Exists(Path.Combine(directory, NebulaMetadataExportService.PendingMarkerFileName));
    }

    private async Task RunMasterLoopAsync(int workerCount, CancellationToken cancellationToken)
    {
        EmitServer("INFO", $"Iniciando scanner de pastas de stage: [{string.Join(", ", _stagingDirs.Select(d => $"'{d}'"))}]");

        // 1. Recoloca uploads interrompidos na fila antes de restaurar a ordem FIFO.
        if (_mongoContext != null)
        {
            var recovered = await _mongoContext.RequeueInterruptedUploadsAsync(cancellationToken).ConfigureAwait(false);
            if (recovered > 0)
            {
                _logger.LogInformation("[NEBULA-WATCHER] {Count} upload(s) interrompido(s) recolocado(s) na fila.", recovered);
            }
        }

        // 2. Restauração imediata de uploads pendentes do MongoDB
        await RestorePendingUploadsFromMongoAsync(cancellationToken).ConfigureAwait(false);

        // Limpeza preventiva de pastas e marcadores órfãos legados
        CleanOrphanStagingDirectories();

        // 3. Inicialização dos workers paralelos de upload
        _workerTasks.Clear();
        for (int i = 1; i <= workerCount; i++)
        {
            var workerId = i;
            _workerTasks.Add(Task.Run(() => UploadWorkerLoopAsync(workerId, cancellationToken), cancellationToken));
        }

        EmitServer("INFO", $"Workers de upload ativos: {workerCount} (configurados={workerCount}, transmissoes={workerCount}, bots={_botCount}).");
        EmitServer("INFO", "Signal handlers unavailable on this platform; use Ctrl+C to stop.");
        EmitServer("INFO", $"Iniciando queued_mongo_scanner ordenado pelo arquivo mais antigo baixado (intervalo=1s, max_por_iteracao={workerCount})");

        // 4. Loop periódico de varredura do Staging e enfileiramento (resync completo a cada 1h, queue check a cada 5s).
        // O intervalo de resync completo foi ampliado de 10 minutos para 1 hora: a limpeza de
        // diretórios órfãos e a resync completa do MongoDB são operações de I/O relativamente
        // pesadas em uma biblioteca de staging grande, e não há necessidade documentada de uma
        // cadência mais curta - novos arquivos continuam sendo detectados imediatamente pelo
        // FileSystemWatcher e pela fila do MongoDB (verificada a cada 5s), então o resync
        // completo só precisa corrigir divergências acumuladas, não novidades.
        var lastFullSync = DateTime.MinValue;
        var fullSyncInterval = TimeSpan.FromHours(1);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_mongoContext != null)
                {
                    if (DateTime.UtcNow - lastFullSync >= fullSyncInterval)
                    {
                        lastFullSync = DateTime.UtcNow;
                        await _mongoContext.SyncStagingDirectoryAsync(_stagingDirs, _uploadEngine.DeleteSourceAfterUpload, cancellationToken).ConfigureAwait(false);
                        CleanOrphanStagingDirectories();
                    }

                    if (_pendingFiles.Count < workerCount * 2)
                    {
                        await RestorePendingUploadsFromMongoAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "[NEBULA-WATCHER] Aviso durante varredura periódica de staging.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RestorePendingUploadsFromMongoAsync(CancellationToken cancellationToken)
    {
        if (_mongoContext == null)
        {
            return;
        }

        try
        {
            var pendingDocs = await _mongoContext.GetActiveOrPendingUploadsAsync(cancellationToken).ConfigureAwait(false);
            var count = 0;

            foreach (var doc in pendingDocs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = doc.TryGetValue("name", out var fn) && fn.IsString ? fn.AsString : null;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    continue;
                }

                var resolvedLocalPath = await _mongoContext.ResolveLocalPathAsync(doc, _stagingDirs, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(resolvedLocalPath) || !File.Exists(resolvedLocalPath))
                {
                    continue;
                }

                if (_queuedFiles.ContainsKey(Path.GetFullPath(resolvedLocalPath)))
                {
                    continue;
                }

                var parent = doc.TryGetValue("parent", out var pVal) && !pVal.IsBsonNull ? pVal.ToString() : null;
                ObjectId? nodeId = doc.TryGetValue("_id", out var idVal) && idVal.IsObjectId
                    ? idVal.AsObjectId
                    : (doc.TryGetValue("_id", out var sidVal) && ObjectId.TryParse(sidVal.ToString(), out var parsedOid) ? parsedOid : null);

                EnqueueFile(resolvedLocalPath, nodeId, parent);
                count++;
            }

            if (count > 0)
            {
                EmitServer("INFO", $"Fila restaurada: {count} arquivo(s) pendente(s)");
                try
                {
                    var (staging, queued, uploading, completed, failed, pending, pendingDisk) = await _mongoContext.GetQueueStatsAsync(cancellationToken).ConfigureAwait(false);
                    EmitServer("INFO", $"Fila: evento=restauracao stage={staging} fila={queued} enviando={uploading} enviados={completed} falhas={failed} faltam={pending} (no disco={pendingDisk})");
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[NEBULA-WATCHER] Falha ao recuperar fila pendente do MongoDB.");
        }
    }

    private async Task UploadWorkerLoopAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("[NEBULA-WATCHER] Worker #{Worker} pronto para envio.", workerId);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_pendingFiles.TryDequeue(out var item))
            {
                var requeued = false;
                try
                {
                    if (IsMetadataSidecar(item.FilePath) && HasPendingMetadataMarker(item.FilePath))
                    {
                        _pendingFiles.Enqueue(item);
                        requeued = true;
                        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    if (File.Exists(item.FilePath) && await IsFileReadyAsync(item.FilePath, cancellationToken).ConfigureAwait(false))
                    {
                        var parentId = item.ParentId;
                        if (_mongoContext != null && string.IsNullOrEmpty(parentId))
                        {
                            var rawRelDir = GetRelativeDirectory(item.FilePath, _stagingDirs);
                            var relDir = NebulaUploadEngine.RouteMediaRelativeDirectory(rawRelDir, item.FileName);
                            if (!string.IsNullOrEmpty(relDir))
                            {
                                parentId = await _mongoContext.EnsureDirectoryStructureAsync(relDir, cancellationToken).ConfigureAwait(false);
                            }
                        }

                        if (_mongoContext != null && item.NodeId.HasValue)
                        {
                            var claimed = await _mongoContext.ClaimFileForUploadAsync(item.NodeId.Value, workerId, 0, cancellationToken).ConfigureAwait(false);
                            if (claimed == null)
                            {
                                _logger.LogInformation("[NEBULA-WATCHER][Worker #{Worker}] Arquivo {File} já foi reivindicado por outro worker; pulando.", workerId, item.FileName);
                                continue;
                            }
                        }
                        else
                        {
                            // Arquivos sem NodeId (descobertos pelo FileSystemWatcher) não possuem
                            // lock no MongoDB. Usamos um claim em memória para evitar que dois
                            // workers processem o mesmo arquivo simultaneamente.
                            var activeKey = Path.GetFullPath(item.FilePath);
                            if (!_activeFiles.TryAdd(activeKey, 0))
                            {
                                _logger.LogDebug("[NEBULA-WATCHER][Worker #{Worker}] Arquivo {File} já está sendo processado por outro worker; pulando.", workerId, item.FileName);
                                continue;
                            }
                        }

                        _logger.LogInformation("[NEBULA-WATCHER][Worker #{Worker}] Processando arquivo para upload: {File} (Parent: {Parent})", workerId, item.FileName, parentId ?? "raiz");
                        await _uploadEngine.ProcessFileUploadAsync(item.FilePath, item.FileName, parentId, workerId, cancellationToken).ConfigureAwait(false);
                    }
                    else if (File.Exists(item.FilePath))
                    {
                        // Arquivo ainda em gravação no disco: reinsere na fila com delay
                        _pendingFiles.Enqueue(item);
                        requeued = true;
                        await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "[NEBULA-WATCHER][Worker #{Worker}] Erro ao processar arquivo: {File}", workerId, item.FilePath);
                }
                finally
                {
                    ReleaseDequeuedItem(_queuedFiles, _activeFiles, item.FilePath, requeued);
                }
            }
            else
            {
                try
                {
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Releases the bookkeeping for one dequeued item.
    /// </summary>
    /// <remarks>
    /// The queue dedupe key must survive when the item goes back into the queue. The previous code
    /// removed it unconditionally from the <c>finally</c>, including on the two paths that re-enqueue
    /// the very same item (a metadata sidecar waiting for its marker, and a file still being written).
    /// Clearing the key there let the watcher enqueue that path a second time while the first copy was
    /// still pending, so the same file could be uploaded twice.
    /// The in-memory claim is always released: for files without a NodeId it is the only mutual
    /// exclusion between workers, and holding it across a requeue would deadlock the item.
    /// </remarks>
    /// <param name="queuedFiles">The queue dedupe map.</param>
    /// <param name="activeFiles">The in-memory claim map.</param>
    /// <param name="filePath">The dequeued file path.</param>
    /// <param name="requeued">Whether the item was put back into the queue.</param>
    private static void ReleaseDequeuedItem(
        ConcurrentDictionary<string, byte> queuedFiles,
        ConcurrentDictionary<string, byte> activeFiles,
        string filePath,
        bool requeued)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!requeued)
        {
            queuedFiles.TryRemove(fullPath, out _);
        }

        activeFiles.TryRemove(fullPath, out _);
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
    }

    private static string GetRelativeDirectory(string fullPath, IEnumerable<string> stagingRoots)
    {
        var fileDir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(fileDir))
        {
            return string.Empty;
        }

        foreach (var root in stagingRoots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var normalizedRoot = Path.GetFullPath(root).TrimEnd('\\', '/');
            var normalizedDir = Path.GetFullPath(fileDir).TrimEnd('\\', '/');

            if (string.Equals(normalizedDir, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                || normalizedDir.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || normalizedDir.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                var rel = normalizedDir[normalizedRoot.Length..].TrimStart('\\', '/').Replace('\\', '/');
                var parts = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var filtered = parts.Where((p, idx) => !(idx == 0 && string.Equals(p, "strm", StringComparison.OrdinalIgnoreCase)));
                return string.Join('/', filtered);
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Para o monitoramento e os workers de upload.
    /// </summary>
    /// <returns>Uma tarefa assíncrona.</returns>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _cts.Cancel();
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();

        if (_masterTask != null)
        {
            try
            {
                await _masterTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Ignora cancelamento
            }
        }

        try
        {
            await Task.WhenAll(_workerTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Workers normally finish through cancellation during shutdown.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[NEBULA-WATCHER] Falha ao aguardar workers durante o encerramento.");
        }

        _isRunning = false;
    }

    private void CleanOrphanStagingDirectories()
    {
        foreach (var stageRoot in _stagingDirs)
        {
            if (string.IsNullOrWhiteSpace(stageRoot) || !Directory.Exists(stageRoot))
            {
                continue;
            }

            try
            {
                // Single bottom-up pass: every directory is visited exactly once (post-order,
                // children before parents) instead of the old O(dirs x files) approach, which
                // re-enumerated the entire subtree of files for every single directory found by
                // Directory.GetDirectories(stageRoot, "*", AllDirectories). By recursing depth
                // first and aggregating the media/active-download/remaining-file flags upward,
                // a directory already knows its subtree's state from its children's results
                // without ever re-scanning a subtree that was already visited.
                CleanDirectorySubtree(stageRoot, isStagingRoot: true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NEBULA-WATCHER] Erro ao varrer diretórios em {Root}", stageRoot);
            }
        }
    }

    /// <summary>
    /// Recursively visits <paramref name="dir"/> bottom-up, deleting it (and removing its
    /// pending marker) when it turns out to be orphaned, and returns the aggregated state used
    /// by the parent call to make the same decision without re-reading any file twice.
    /// </summary>
    private (bool HasMedia, bool HasActiveDownload, int RemainingFileCount) CleanDirectorySubtree(string dir, bool isStagingRoot)
    {
        var hasMedia = false;
        var hasActiveDownload = false;
        var remainingFileCount = 0;

        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(dir);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-WATCHER] Não foi possível enumerar diretório: {Dir}", dir);

            // Treat as non-orphan on enumeration failure so we never delete something we
            // could not actually inspect.
            return (true, true, 1);
        }

        foreach (var entry in entries)
        {
            if (Directory.Exists(entry))
            {
                var childResult = CleanDirectorySubtree(entry, isStagingRoot: false);
                hasMedia |= childResult.HasMedia;
                hasActiveDownload |= childResult.HasActiveDownload;
                remainingFileCount += childResult.RemainingFileCount;
                continue;
            }

            var isMarker = string.Equals(Path.GetFileName(entry), NebulaMetadataExportService.PendingMarkerFileName, StringComparison.OrdinalIgnoreCase);
            if (!isMarker)
            {
                remainingFileCount++;
            }

            if (NebulaMetadataExportService.IsMediaPayloadPath(entry))
            {
                hasMedia = true;
            }
            else if (entry.EndsWith(".part", StringComparison.OrdinalIgnoreCase)
                || entry.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
                || entry.EndsWith(".download", StringComparison.OrdinalIgnoreCase))
            {
                hasActiveDownload = true;
            }
        }

        // The staging root itself is never a candidate for marker removal or deletion; only
        // its descendants are, exactly like the previous GetDirectories(..., AllDirectories)
        // enumeration which never included the root path.
        if (!isStagingRoot)
        {
            try
            {
                if (!hasMedia && !hasActiveDownload)
                {
                    NebulaMetadataExportService.RemovePendingMarker(dir);
                }

                if (remainingFileCount == 0)
                {
                    NebulaMetadataExportService.RemovePendingMarker(dir);
                    if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir, true);
                        _logger.LogInformation("[NEBULA-WATCHER] Diretório órfão de staging removido: {Dir}", dir);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NEBULA-WATCHER] Não foi possível remover diretório órfão: {Dir}", dir);
            }
        }

        return (hasMedia, hasActiveDownload, remainingFileCount);
    }


    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _cts.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class NebulaUploadTaskItem
    {
        public required string FilePath { get; init; }

        public required string FileName { get; init; }

        public string? ParentId { get; init; }

        public ObjectId? NodeId { get; init; }
    }
}
