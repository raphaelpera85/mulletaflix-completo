using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Providers.Plugins.MidiaStorageOnline;

/// <summary>
/// Builds the manifest update and stale-file cleanup list for an M3U sync.
/// </summary>
internal static class MidiaStorageOnlineManifestCleanup
{
    public static MidiaStorageOnlineManifestCleanupPlan CreatePlan(
        IReadOnlyDictionary<string, string> previousManifest,
        IReadOnlyDictionary<string, string> currentManifest,
        bool force,
        bool mediaSourceIncomplete)
    {
        ArgumentNullException.ThrowIfNull(previousManifest);
        ArgumentNullException.ThrowIfNull(currentManifest);

        var nextManifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (mediaSourceIncomplete)
        {
            foreach (var (path, url) in previousManifest)
            {
                nextManifest[path] = url;
            }
        }

        foreach (var (path, url) in currentManifest)
        {
            nextManifest[path] = url;
        }

        var pathsToDelete = force || mediaSourceIncomplete
            ? Array.Empty<string>()
            : previousManifest.Keys
                .Except(currentManifest.Keys, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new MidiaStorageOnlineManifestCleanupPlan(nextManifest, pathsToDelete);
    }
}

internal sealed record MidiaStorageOnlineManifestCleanupPlan(
    Dictionary<string, string> ManifestToSave,
    IReadOnlyList<string> PathsToDelete);
