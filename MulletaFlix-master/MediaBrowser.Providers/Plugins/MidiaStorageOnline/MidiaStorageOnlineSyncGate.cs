using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.Plugins.MidiaStorageOnline;

/// <summary>
/// Serializes media syncs that share a manifest file.
/// </summary>
internal static class MidiaStorageOnlineSyncGate
{
    private static readonly TimeSpan MaximumWait = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>
    /// Acquires an exclusive lease for the manifest path.
    /// </summary>
    /// <param name="manifestPath">Path to the shared sync manifest.</param>
    /// <param name="cancellationToken">Token used to cancel the wait.</param>
    /// <returns>A lease that releases the gate when disposed.</returns>
    public static async Task<IDisposable> AcquireAsync(string manifestPath, CancellationToken cancellationToken)
        => await AcquireAsync(manifestPath, cancellationToken, MaximumWait).ConfigureAwait(false);

    internal static async Task<IDisposable> AcquireAsync(string manifestPath, CancellationToken cancellationToken, TimeSpan maximumWait)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumWait, TimeSpan.Zero);

        var key = Path.GetFullPath(manifestPath);
        var semaphore = Gates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        if (!await semaphore.WaitAsync(maximumWait, cancellationToken).ConfigureAwait(false))
        {
            throw new TimeoutException($"A sincronização do manifesto '{key}' aguardou mais de {maximumWait.TotalMinutes:0} minutos pela execução atual.");
        }

        return new Lease(semaphore);
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
