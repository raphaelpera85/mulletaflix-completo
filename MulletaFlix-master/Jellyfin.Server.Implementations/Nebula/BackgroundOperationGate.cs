using System;
using System.Threading;
using System.Threading.Tasks;

namespace MulletaFlix.Server.Implementations.Nebula;

/// <summary>
/// Admits one fire-and-forget operation until its actual asynchronous work completes.
/// </summary>
internal sealed class BackgroundOperationGate
{
    private int _isRunning;

    /// <summary>
    /// Starts an operation if no operation currently owns the gate.
    /// </summary>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="onFailure">Callback for failures that would otherwise be unobserved.</param>
    /// <returns>The running task, or <see langword="null"/> when another operation owns the gate.</returns>
    public Task? TryStart(Func<Task> operation, Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(onFailure);

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return null;
        }

        return Task.Run(async () =>
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                onFailure(ex);
            }
            finally
            {
                Volatile.Write(ref _isRunning, 0);
            }
        });
    }
}
