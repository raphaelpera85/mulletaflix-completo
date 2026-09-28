using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Coalesces completed uploads by virtual directory before refreshing rclone's VFS listing.
/// </summary>
internal sealed class NebulaDirectoryRefreshQueue : IDisposable
{
    private sealed class DirectoryState
    {
        public CancellationTokenSource Cancellation { get; set; } = null!;

        public bool IsRefreshing { get; set; }

        public bool RefreshAgain { get; set; }
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, DirectoryState> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, CancellationToken, Task> _refresh;
    private readonly ILogger<NebulaDirectoryRefreshQueue> _logger;
    private readonly TimeSpan _debounce;
    private readonly CancellationTokenSource _shutdown = new();
    private bool _disposed;

    public NebulaDirectoryRefreshQueue(
        Func<string, CancellationToken, Task> refresh,
        ILogger<NebulaDirectoryRefreshQueue> logger,
        TimeSpan? debounce = null)
    {
        _refresh = refresh;
        _logger = logger;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(750);
    }

    public void Enqueue(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        DirectoryState state;
        CancellationTokenSource? superseded = null;
        var startWorker = false;
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            if (!_directories.TryGetValue(directory, out state!))
            {
                state = new DirectoryState();
                _directories[directory] = state;
                startWorker = true;
            }
            else if (state.IsRefreshing)
            {
                state.RefreshAgain = true;
                return;
            }
            else
            {
                superseded = state.Cancellation;
            }

            state.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        }

        superseded?.Cancel();
        superseded?.Dispose();
        if (startWorker)
        {
            _ = RefreshAfterQuietPeriodAsync(directory, state);
        }
    }

    private async Task RefreshAfterQuietPeriodAsync(string directory, DirectoryState state)
    {
        while (true)
        {
            CancellationTokenSource cancellation;
            lock (_lock)
            {
                if (_disposed || !_directories.TryGetValue(directory, out var current) || !ReferenceEquals(current, state))
                {
                    if (_disposed)
                    {
                        state.Cancellation.Dispose();
                    }

                    return;
                }

                cancellation = state.Cancellation;
            }

            try
            {
                await Task.Delay(_debounce, cancellation.Token).ConfigureAwait(false);
                lock (_lock)
                {
                    if (_disposed || !_directories.TryGetValue(directory, out var current) || !ReferenceEquals(current, state)
                        || !ReferenceEquals(state.Cancellation, cancellation))
                    {
                        continue;
                    }

                    state.IsRefreshing = true;
                }

                await _refresh(directory, cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                // A newer upload superseded the debounce or the manager is shutting down.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao atualizar cache rclone da pasta virtual {Directory}.", directory);
            }

            lock (_lock)
            {
                if (!_directories.TryGetValue(directory, out var current) || !ReferenceEquals(current, state))
                {
                    cancellation.Dispose();
                    return;
                }

                if (!ReferenceEquals(state.Cancellation, cancellation))
                {
                    // Enqueue replaced a debounce timer while it was waiting.
                    continue;
                }

                state.IsRefreshing = false;
                cancellation.Dispose();
                if (!state.RefreshAgain || _disposed)
                {
                    _directories.Remove(directory);
                    return;
                }

                state.RefreshAgain = false;
                state.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shutdown.Cancel();
            foreach (var state in _directories.Values)
            {
                state.Cancellation.Cancel();
            }

            _directories.Clear();
        }

        _shutdown.Dispose();
    }
}
