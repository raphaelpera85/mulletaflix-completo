using System;
using System.Collections.Generic;
using System.Linq;

namespace MediaBrowser.Common.Updates;

/// <summary>
/// Selects the server updater archive from release assets.
/// </summary>
public static class ServerUpdateArchiveSelector
{
    /// <summary>
    /// The canonical name used by the Windows server update package.
    /// </summary>
    public const string UpdateArchiveAssetName = "mulletaflix-update-win-x64.zip";

    /// <summary>
    /// Selects the canonical updater archive, or a sole ZIP asset for legacy releases.
    /// </summary>
    /// <typeparam name="TAsset">The release asset type.</typeparam>
    /// <param name="assets">Release assets.</param>
    /// <param name="getName">Returns an asset's name.</param>
    /// <returns>The selected asset, or <see langword="null"/> when absent or ambiguous.</returns>
    public static TAsset? Select<TAsset>(IEnumerable<TAsset>? assets, Func<TAsset, string?> getName)
        where TAsset : class
    {
        if (assets is null)
        {
            return null;
        }

        var zipAssets = assets.Where(asset =>
        {
            var name = getName(asset);
            return name is not null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }).ToArray();

        return zipAssets.FirstOrDefault(asset => string.Equals(getName(asset), UpdateArchiveAssetName, StringComparison.OrdinalIgnoreCase))
            ?? (zipAssets.Length == 1 ? zipAssets[0] : null);
    }
}
