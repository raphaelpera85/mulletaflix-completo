using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Renewable cross-process Mongo admission lease for a Nebula upload or Supabase sync.
/// </summary>
internal sealed class NebulaMongoAdmissionLease : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<bool>> _renew;
    private readonly Func<CancellationToken, Task> _release;
    private readonly CancellationTokenSource _heartbeatCancellation = new();
    private readonly CancellationTokenSource _lostCancellation = new();
    private readonly Task _heartbeatTask;
    private int _disposed;

    public NebulaMongoAdmissionLease(
        Func<CancellationToken, Task<bool>> renew,
        Func<CancellationToken, Task> release,
        TimeSpan heartbeatInterval)
    {
        _renew = renew ?? throw new ArgumentNullException(nameof(renew));
        _release = release ?? throw new ArgumentNullException(nameof(release));
        if (heartbeatInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(heartbeatInterval));
        }

        _heartbeatTask = RunHeartbeatAsync(heartbeatInterval);
    }

    public CancellationToken LeaseLostToken => _lostCancellation.Token;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _heartbeatCancellation.Cancel();
        try
        {
            await _heartbeatTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_heartbeatCancellation.IsCancellationRequested)
        {
        }

        try
        {
            await _release(CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _heartbeatCancellation.Dispose();
            _lostCancellation.Dispose();
        }
    }

    private async Task RunHeartbeatAsync(TimeSpan heartbeatInterval)
    {
        using var timer = new PeriodicTimer(heartbeatInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(_heartbeatCancellation.Token).ConfigureAwait(false))
            {
                if (!await _renew(_heartbeatCancellation.Token).ConfigureAwait(false))
                {
                    _lostCancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (_heartbeatCancellation.IsCancellationRequested)
        {
        }
        catch
        {
            _lostCancellation.Cancel();
        }
    }
}
