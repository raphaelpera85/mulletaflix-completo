using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using Jellyfin.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Covers the upload queue bookkeeping that decides whether a file can be enqueued twice.
/// </summary>
/// <remarks>
/// The bug these tests pin down: the worker loop removed the queue dedupe key unconditionally in its
/// <c>finally</c>, including on the two paths that put the very same item back into the queue (a
/// metadata sidecar waiting for its marker, and a file still being written to disk). With the key
/// gone, the staging watcher could enqueue that path again while the first copy was still pending,
/// and the same file could be uploaded twice — real bandwidth and Telegram quota spent twice.
/// </remarks>
public sealed class NebulaStagingWatcherTests
{
    private static readonly string StagedFile = Path.Combine(
        Path.GetTempPath(),
        "nebula-watcher-tests",
        "Season 01",
        "episode01.mkv");

    [Fact]
    public void RequeuedItem_KeepsTheQueueDedupeKey()
    {
        var queued = CreateMap(StagedFile);
        var active = CreateMap(StagedFile);

        Release(queued, active, StagedFile, requeued: true);

        Assert.True(
            queued.ContainsKey(Path.GetFullPath(StagedFile)),
            "A requeued item must keep its dedupe key, otherwise the same file can be enqueued again.");
    }

    [Fact]
    public void RequeuedItem_AlwaysReleasesTheInMemoryClaim()
    {
        var queued = CreateMap(StagedFile);
        var active = CreateMap(StagedFile);

        Release(queued, active, StagedFile, requeued: true);

        // Holding the claim across a requeue would make the next TryAdd fail forever, so the item
        // would never be processed again.
        Assert.False(active.ContainsKey(Path.GetFullPath(StagedFile)));
    }

    [Fact]
    public void TerminalItem_ClearsBothKeysSoTheFileCanBeDiscoveredAgain()
    {
        var queued = CreateMap(StagedFile);
        var active = CreateMap(StagedFile);

        Release(queued, active, StagedFile, requeued: false);

        Assert.False(queued.ContainsKey(Path.GetFullPath(StagedFile)));
        Assert.False(active.ContainsKey(Path.GetFullPath(StagedFile)));
    }

    [Fact]
    public void RequeueThenTerminal_ClearsTheKeyOnlyAtTheEnd()
    {
        var queued = CreateMap(StagedFile);
        var active = CreateMap(StagedFile);

        // Two requeues in a row, as happens while a sidecar waits for its metadata marker.
        Release(queued, active, StagedFile, requeued: true);
        Release(queued, active, StagedFile, requeued: true);

        Assert.True(queued.ContainsKey(Path.GetFullPath(StagedFile)));

        // Then the item finally completes.
        Release(queued, active, StagedFile, requeued: false);

        Assert.False(queued.ContainsKey(Path.GetFullPath(StagedFile)));
    }

    private static ConcurrentDictionary<string, byte> CreateMap(string path)
    {
        var map = new ConcurrentDictionary<string, byte>(System.StringComparer.OrdinalIgnoreCase);
        map[Path.GetFullPath(path)] = 0;
        return map;
    }

    private static void Release(
        ConcurrentDictionary<string, byte> queued,
        ConcurrentDictionary<string, byte> active,
        string filePath,
        bool requeued)
    {
        var method = typeof(NebulaStagingWatcher).GetMethod(
            "ReleaseDequeuedItem",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        method.Invoke(null, [queued, active, filePath, requeued]);
    }
}
