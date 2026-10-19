using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Runs background work once immediately, then again after each successful interval tick.
/// </summary>
internal static class NebulaPeriodicLoop
{
    public static async Task RunImmediatelyThenPeriodicallyAsync(
        Func<CancellationToken, Task> work,
        Func<CancellationToken, ValueTask<bool>> waitForNextTick,
        CancellationToken cancellationToken)
    {
        await work(cancellationToken).ConfigureAwait(false);
        while (await waitForNextTick(cancellationToken).ConfigureAwait(false))
        {
            await work(cancellationToken).ConfigureAwait(false);
        }
    }
}
