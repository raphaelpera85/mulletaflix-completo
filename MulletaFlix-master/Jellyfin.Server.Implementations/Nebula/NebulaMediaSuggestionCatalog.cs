using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Server.Implementations.Nebula;

/// <summary>Serves an immutable STRM catalog while refreshing it off the request path.</summary>
internal sealed class NebulaMediaSuggestionCatalog : IAsyncDisposable
{
    private static readonly TimeSpan CatalogCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan EmptyCatalogCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan FailedScanRetryDelay = TimeSpan.FromSeconds(30);
    private readonly object _gate = new();
    private readonly Func<string[]> _getRoots;
    private readonly Func<IEnumerable<string>, CancellationToken, NebulaMediaSuggestionCatalogBuildResult> _buildCatalog;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _shutdown = new();
    private IReadOnlyList<NebulaMediaSuggestionDto> _items = Array.Empty<NebulaMediaSuggestionDto>();
    private string _rootSignature = string.Empty;
    private string _state = "NotStarted";
    private DateTime _lastAttemptAtUtc = DateTime.MinValue;
    private DateTime? _lastIndexedAtUtc;
    private int _rootCount;
    private int _failedRootCount;
    private Task _refreshTask = Task.CompletedTask;
    private bool _disposed;

    public NebulaMediaSuggestionCatalog(
        Func<string[]> getRoots,
        Func<IEnumerable<string>, CancellationToken, NebulaMediaSuggestionCatalogBuildResult> buildCatalog,
        ILogger logger)
    {
        _getRoots = getRoots;
        _buildCatalog = buildCatalog;
        _logger = logger;
    }

    public IReadOnlyList<NebulaMediaSuggestionDto> GetItems()
    {
        EnsureRefreshStarted();
        lock (_gate)
        {
            return _items;
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Task currentRefresh;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            currentRefresh = _refreshTask;
        }

        await currentRefresh.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _rootSignature = string.Empty;
            _lastAttemptAtUtc = DateTime.MinValue;
        }

        EnsureRefreshStarted();
        Task requestedRefresh;
        lock (_gate)
        {
            requestedRefresh = _refreshTask;
        }

        await requestedRefresh.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public NebulaMediaSuggestionIndexStatusDto GetStatus()
    {
        EnsureRefreshStarted();
        lock (_gate)
        {
            return new NebulaMediaSuggestionIndexStatusDto
            {
                State = _state,
                IsIndexing = _state == "Indexing",
                IndexedTitleCount = _items.Count,
                RootCount = _rootCount,
                FailedRootCount = _failedRootCount,
                LastIndexedAtUtc = _lastIndexedAtUtc
            };
        }
    }

    private void EnsureRefreshStarted()
    {
        string[] roots;
        try
        {
            roots = _getRoots();
        }
        catch (Exception ex)
        {
            var shouldLog = false;
            lock (_gate)
            {
                if (!_disposed && _state != "Indexing"
                    && (_state != "Error" || DateTime.UtcNow - _lastAttemptAtUtc >= FailedScanRetryDelay))
                {
                    _state = "Error";
                    _lastAttemptAtUtc = DateTime.UtcNow;
                    shouldLog = true;
                }
            }

            if (shouldLog)
            {
                _logger.LogWarning(ex, "[NEBULA-REQUESTS] Falha ao descobrir raízes do índice STRM.");
            }

            return;
        }

        roots = roots.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
        var signature = string.Join("\n", roots);
        lock (_gate)
        {
            if (_disposed || _state == "Indexing")
            {
                return;
            }

            var now = DateTime.UtcNow;
            var cacheDuration = _items.Count == 0 ? EmptyCatalogCacheDuration : CatalogCacheDuration;
            var intervalElapsed = _state == "Error"
                ? now - _lastAttemptAtUtc >= FailedScanRetryDelay
                : now - _lastAttemptAtUtc >= cacheDuration;
            if (string.Equals(_rootSignature, signature, StringComparison.Ordinal) && !intervalElapsed)
            {
                return;
            }

            _state = "Indexing";
            _rootSignature = signature;
            _rootCount = roots.Length;
            _lastAttemptAtUtc = now;
            _refreshTask = Task.Run(() => Refresh(roots, _shutdown.Token));
        }
    }

    private void Refresh(string[] roots, CancellationToken cancellationToken)
    {
        try
        {
            var result = _buildCatalog(roots, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _items = result.Items.ToArray();
                _failedRootCount = result.FailedRootCount;
                _lastIndexedAtUtc = DateTime.UtcNow;
                _state = _failedRootCount == 0 ? "Ready" : "ReadyWithWarnings";
            }

            _logger.LogInformation(
                "[NEBULA-REQUESTS] Índice STRM concluído: {TitleCount} títulos, {RootCount} raízes, {FailedRootCount} raízes com falha.",
                result.Items.Count,
                roots.Length,
                result.FailedRootCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    _state = _lastIndexedAtUtc.HasValue ? "Ready" : "NotStarted";
                }
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    _state = "Error";
                }
            }

            _logger.LogError(ex, "[NEBULA-REQUESTS] Falha ao atualizar o índice STRM; catálogo anterior preservado.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        Task refreshTask;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shutdown.Cancel();
            refreshTask = _refreshTask;
        }

        try
        {
            await refreshTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        _shutdown.Dispose();
    }
}

internal sealed record NebulaMediaSuggestionCatalogBuildResult(
    IReadOnlyList<NebulaMediaSuggestionDto> Items,
    int FailedRootCount);
