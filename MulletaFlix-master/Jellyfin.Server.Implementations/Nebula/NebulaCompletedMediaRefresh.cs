using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using MongoDB.Bson;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Maps recently completed Mongo media records to virtual directories in the N: rclone mount.
/// </summary>
internal static class NebulaCompletedMediaRefresh
{
    internal static DateTimeOffset GetCompletedSince(DateTimeOffset now)
        => now.Subtract(TimeSpan.FromMinutes(30));

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

    internal static IReadOnlyCollection<string> GetRefreshAncestors(string directory)
    {
        var segments = NebulaUploadEngine.SplitPathSegments(directory);
        if (segments.Length == 0 || !NebulaUploadEngine.IsVisibleCategoryRoot(segments[0]))
        {
            return Array.Empty<string>();
        }

        var ancestors = new List<string> { "/" };
        for (var index = 1; index <= segments.Length; index++)
        {
            ancestors.Add("/" + string.Join('/', segments, 0, index));
        }

        return ancestors;
    }

    internal static string? GetRcloneRefreshError(JsonElement root, string expectedDirectory)
    {
        if (root.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(error.GetString()))
        {
            return error.GetString();
        }

        // rclone reports per-directory failures in `result`, even when its RC call
        // returns HTTP 200. Those failures must not be mistaken for a successful refresh.
        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
        {
            return "rclone não retornou o mapa result esperado";
        }

        if (!result.TryGetProperty(expectedDirectory, out var directoryResult)
            || directoryResult.ValueKind != JsonValueKind.String)
        {
            return $"rclone não retornou resultado para o diretório {expectedDirectory}";
        }

        var resultText = directoryResult.GetString();
        return string.Equals(resultText, "OK", StringComparison.OrdinalIgnoreCase)
            ? null
            : resultText ?? "rclone retornou resultado vazio para o diretório";
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
