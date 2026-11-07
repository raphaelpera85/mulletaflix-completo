using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Providers.Plugins.MidiaStorageOnline;

internal static class MidiaStorageOnlineSyncGate
{
    private static readonly TimeSpan MaximumWait = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    internal static Task RunAsync(string manifestPath, Func<Task> operation, CancellationToken cancellationToken)
        => RunAsync(
            manifestPath,
            async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    internal static async Task<T> RunAsync<T>(string manifestPath, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var key = Path.GetFullPath(manifestPath);
        var gate = Gates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(MaximumWait, cancellationToken).ConfigureAwait(false))
        {
            throw new TimeoutException($"A sincronização do manifesto '{key}' aguardou mais de {MaximumWait.TotalMinutes:0} minutos pela execução atual.");
        }

        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
