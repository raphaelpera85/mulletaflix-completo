using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Coalesces completed uploads by virtual directory and refreshes rclone sequentially.
/// </summary>
internal sealed class NebulaDirectoryRefreshQueue : IAsyncDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<string, DateTimeOffset> _pendingDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, CancellationToken, Task> _refresh;
    private readonly ILogger<NebulaDirectoryRefreshQueue> _logger;
    private readonly TimeSpan _debounce;
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _workerTask;
    private bool _disposed;
    private int _resourcesDisposed;

    public NebulaDirectoryRefreshQueue(
        Func<string, CancellationToken, Task> refresh,
        ILogger<NebulaDirectoryRefreshQueue> logger,
        TimeSpan? debounce = null)
    {
        _refresh = refresh;
        _logger = logger;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(750);
        _workerTask = ProcessQueueAsync();
    }

    public void Enqueue(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _pendingDirectories[directory] = DateTimeOffset.UtcNow + _debounce;
        }

        try
        {
            _wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake-up is already pending; the worker will observe the latest due time.
        }
    }

    private async Task ProcessQueueAsync()
    {
        var cancellationToken = _shutdown.Token;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? directory = null;
                TimeSpan? wait = null;

                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    var next = default(KeyValuePair<string, DateTimeOffset>?);
                    foreach (var pending in _pendingDirectories)
                    {
                        if (next is null || pending.Value < next.Value.Value)
                        {
                            next = pending;
                        }
                    }

                    if (next is { } scheduled)
                    {
                        var remaining = scheduled.Value - DateTimeOffset.UtcNow;
                        if (remaining <= TimeSpan.Zero)
                        {
                            directory = scheduled.Key;
                            _pendingDirectories.Remove(directory);
                        }
                        else
                        {
                            wait = remaining;
                        }
                    }
                }

                if (directory is null)
                {
                    if (wait is { } delay)
                    {
                        await _wakeSignal.WaitAsync(delay, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await _wakeSignal.WaitAsync(cancellationToken).ConfigureAwait(false);
                    }

                    continue;
                }

                try
                {
                    await _refresh(directory, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao atualizar cache rclone da pasta virtual {Directory}.", directory);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown cancels a pending debounce or the active rclone refresh.
        }
    }

    private void BeginShutdown()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pendingDirectories.Clear();
        }

        _shutdown.Cancel();
    }

    public async ValueTask DisposeAsync()
    {
        BeginShutdown();
        await _workerTask.ConfigureAwait(false);

        if (Interlocked.Exchange(ref _resourcesDisposed, 1) == 0)
        {
            _wakeSignal.Dispose();
            _shutdown.Dispose();
        }
    }
}
