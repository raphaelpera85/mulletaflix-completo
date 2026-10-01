using System;
using System.Threading;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Admits one library-structure background refresh until its actual work completes.
/// </summary>
internal static class LibraryBackgroundOperationGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>
    /// Attempts to reserve the library refresh slot without waiting.
    /// </summary>
    /// <returns>A disposable lease, or <see langword="null"/> when a refresh is active.</returns>
    public static IDisposable? TryAcquire()
    {
        return Gate.Wait(0) ? new Lease(Gate) : null;
    }

    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    {
        private SemaphoreSlim? _gate = gate;

        public void Dispose()
        {
            Interlocked.Exchange(ref _gate, null)?.Release();
        }
    }
}
