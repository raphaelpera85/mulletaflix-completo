using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Maps recently completed Mongo media records to virtual directories in the N: rclone mount.
/// </summary>
internal static class NebulaCompletedMediaRefresh
{
    public static async Task RunImmediatelyThenPeriodicallyAsync(
        Func<CancellationToken, Task> refresh,
        Func<CancellationToken, ValueTask<bool>> waitForNextTick,
        CancellationToken cancellationToken)
    {
        await refresh(cancellationToken).ConfigureAwait(false);
        while (await waitForNextTick(cancellationToken).ConfigureAwait(false))
        {
            await refresh(cancellationToken).ConfigureAwait(false);
        }
    }

    public static IReadOnlyCollection<string> GetVirtualDirectories(
        IEnumerable<BsonDocument> completedFiles,
        IReadOnlyDictionary<string, string> directoryPathMap)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in completedFiles)
        {
            var name = file.GetValue("name", string.Empty).AsString;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var parent = file.GetValue("parent", BsonNull.Value);
            var relativeDirectory = string.Empty;
            if (!parent.IsBsonNull)
            {
                if (directoryPathMap.TryGetValue(parent.ToString(), out var parentPath))
                {
                    relativeDirectory = parentPath;
                }
                else if (parent.IsString)
                {
                    relativeDirectory = ResolveLegacyParentPath(parent.AsString);
                }
                else
                {
                    continue;
                }
            }

            var routedDirectory = NebulaUploadEngine.RouteMediaRelativeDirectory(relativeDirectory, name);
            directories.Add("/" + routedDirectory.Trim('/'));
        }

        return directories;
    }

    private static string ResolveLegacyParentPath(string parentPath)
    {
        var segments = NebulaUploadEngine.SplitPathSegments(parentPath);
        var categoryRootIndex = Array.FindLastIndex(segments, NebulaUploadEngine.IsVisibleCategoryRoot);
        return categoryRootIndex < 0
            ? string.Join('/', segments)
            : string.Join('/', segments, categoryRootIndex, segments.Length - categoryRootIndex);
    }
}
