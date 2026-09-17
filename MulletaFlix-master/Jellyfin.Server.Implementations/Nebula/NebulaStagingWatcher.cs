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

        // 4. Loop periódico de varredura do Staging e enfileiramento (resync completo a cada 10 min, queue check a cada 5s)
        var lastFullSync = DateTime.MinValue;
        var fullSyncInterval = TimeSpan.FromMinutes(10);

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
                try
                {
                    if (IsMetadataSidecar(item.FilePath) && HasPendingMetadataMarker(item.FilePath))
                    {
                        _pendingFiles.Enqueue(item);
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
                        await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "[NEBULA-WATCHER][Worker #{Worker}] Erro ao processar arquivo: {File}", workerId, item.FilePath);
                }
                finally
                {
                    var fullPath = Path.GetFullPath(item.FilePath);
                    _queuedFiles.TryRemove(fullPath, out _);
                    _activeFiles.TryRemove(fullPath, out _);
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
