using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>Owns a single cancellable automatic mount attempt.</summary>
internal sealed class NebulaMountRetry : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<CancellationToken, Task> _mount;
    private readonly ILogger<NebulaMountRetry> _logger;
    private CancellationTokenSource? _current;
    private Task _pending = Task.CompletedTask;
    private Task _cancelCallbacks = Task.CompletedTask;
    private TimeSpan? _rescheduleDelay;
    private bool _disposed;
    private bool _finishing;

    public NebulaMountRetry(Func<CancellationToken, Task> mount, ILogger<NebulaMountRetry> logger)
    {
        _mount = mount;
        _logger = logger;
    }

    public void Schedule(TimeSpan delay)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_current != null)
            {
                if (!_finishing && !_current.IsCancellationRequested)
                {
                    _rescheduleDelay ??= delay;
                }

                return;
            }

            StartAttempt(delay);
        }
    }

    private void StartAttempt(TimeSpan delay)
    {
            var source = new CancellationTokenSource();
            _current = source;
            _cancelCallbacks = Task.CompletedTask;
            _pending = Task.Run(() => RunAsync(source, delay));
    }

    public void Cancel()
    {
        lock (_gate)
        {
            _rescheduleDelay = null;
            if (_current != null && !_finishing && !_current.IsCancellationRequested)
            {
                _cancelCallbacks = _current.CancelAsync();
            }
        }
    }

    internal async Task WaitForIdleAsync()
    {
        while (true)
        {
            Task pending;
            lock (_gate)
            {
                pending = _pending;
            }

            await pending.ConfigureAwait(false);
            lock (_gate)
            {
                if (ReferenceEquals(pending, _pending))
                {
                    return;
                }
            }
        }
    }

    private async Task RunAsync(CancellationTokenSource source, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, source.Token).ConfigureAwait(false);
            source.Token.ThrowIfCancellationRequested();
            await _mount(source.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            // Explicit shutdown/unmount is not a mount failure or a new retry.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-MOUNT] Automatic mount attempt failed.");
        }
        finally
        {
            Task callbacks;
            lock (_gate)
            {
                _finishing = true;
                callbacks = _cancelCallbacks;
            }

            try
            {
                await callbacks.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEBULA-MOUNT] Mount cancellation callback failed.");
            }

            lock (_gate)
            {
                _current = null;
                _finishing = false;
                source.Dispose();
                if (_rescheduleDelay is { } nextDelay && !_disposed)
                {
                    _rescheduleDelay = null;
                    StartAttempt(nextDelay);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            Cancel();
        }
    }
}
