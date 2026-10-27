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
    private const int MaximumRetryDelayMinutes = 5;
    private readonly object _lock = new();
    private readonly Dictionary<string, (DateTimeOffset DueAt, int Attempt)> _pendingDirectories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, CancellationToken, Task> _refresh;
    private readonly ILogger<NebulaDirectoryRefreshQueue> _logger;
    private readonly TimeSpan _debounce;
    private readonly TimeSpan _retryBaseDelay;
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _workerTask;
    private bool _disposed;
    private int _resourcesDisposed;

    public NebulaDirectoryRefreshQueue(
        Func<string, CancellationToken, Task> refresh,
        ILogger<NebulaDirectoryRefreshQueue> logger,
        TimeSpan? debounce = null,
        TimeSpan? retryBaseDelay = null)
    {
        _refresh = refresh;
        _logger = logger;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(750);
        _retryBaseDelay = retryBaseDelay ?? TimeSpan.FromSeconds(5);
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

            _pendingDirectories[directory] = (DateTimeOffset.UtcNow + _debounce, 0);
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
                var attempt = 0;
                TimeSpan? wait = null;

                lock (_lock)
                {
                    if (_disposed)
                    {
                        return;
                    }

                    var next = default(KeyValuePair<string, (DateTimeOffset DueAt, int Attempt)>?);
                    foreach (var pending in _pendingDirectories)
                    {
                        if (next is null || pending.Value.DueAt < next.Value.Value.DueAt)
                        {
                            next = pending;
                        }
                    }

                    if (next is { } scheduled)
                    {
                        var remaining = scheduled.Value.DueAt - DateTimeOffset.UtcNow;
                        if (remaining <= TimeSpan.Zero)
                        {
                            directory = scheduled.Key;
                            attempt = scheduled.Value.Attempt;
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
                    ScheduleRetry(directory, attempt + 1);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown cancels a pending debounce or the active rclone refresh.
        }
    }

    private void ScheduleRetry(string directory, int attempt)
    {
        var exponent = Math.Min(attempt - 1, 6);
        var delay = TimeSpan.FromTicks(Math.Min(
            _retryBaseDelay.Ticks * (1L << exponent),
            TimeSpan.FromMinutes(MaximumRetryDelayMinutes).Ticks));

        lock (_lock)
        {
            if (_disposed || _pendingDirectories.ContainsKey(directory))
            {
                return;
            }

            _pendingDirectories[directory] = (DateTimeOffset.UtcNow + delay, attempt);
        }

        try
        {
            _wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A wake-up is already pending; the worker will observe the retry due time.
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
