using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Updates;

namespace Emby.Server.Implementations.Plugins;

internal static class BootstrapPluginCatalog
{
    internal static readonly RepositoryInfo[] DefaultPluginRepositories =
    [
        new RepositoryInfo
        {
            Name = "Jellyfin Plugin Manifest",
            Url = "https://raw.githubusercontent.com/danieladov/JellyfinPluginManifest/master/manifest.json",
            Enabled = true
        },
        new RepositoryInfo
        {
            Name = "Jellyfin Enhanced Plugins",
            Url = "https://raw.githubusercontent.com/n00bcodr/jellyfin-plugins/main/10.11/manifest.json",
            Enabled = true
        },
        new RepositoryInfo
        {
            Name = "IAmParadox Plugins",
            Url = "https://www.iamparadox.dev/jellyfin/plugins/manifest.json",
            Enabled = true
        }
    ];

    internal static readonly BootstrapRepositorySpec[] BootstrapRepositories =
    [
        new(
            "https://raw.githubusercontent.com/danieladov/JellyfinPluginManifest/master/manifest.json",
            // Do not install third-party plugins automatically. The published
            // Merge Versions and Theme Songs packages currently reference
            // server APIs that are not compatible with this build, so a clean
            // startup must not seed them into the user's data directory.
            []),
        new(
            "https://raw.githubusercontent.com/n00bcodr/jellyfin-plugins/main/10.11/manifest.json",
            [])
        // IAmParadox plugins removed from bootstrap due to circular dependency in File Transformation plugin
        // Users can install them manually from the repository if needed
    ];

    internal sealed record BootstrapRepositorySpec(string Url, Guid[] PluginIds);

    internal static IEnumerable<string> GetManifestCandidates(string repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            yield break;
        }

        yield return repositoryUrl;

        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri))
        {
            yield break;
        }

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            yield break;
        }

        var owner = segments[0];
        var repository = segments[1];
        foreach (var branch in new[] { "main", "master" })
        {
            yield return $"https://raw.githubusercontent.com/{owner}/{repository}/{branch}/manifest.json";
            yield return $"https://raw.githubusercontent.com/{owner}/{repository}/{branch}/repository.json";
        }
    }

    internal static bool MatchesRepositoryUrl(string configuredUrl, string bootstrapUrl)
    {
        return GetManifestCandidates(configuredUrl)
            .Any(candidate => string.Equals(
                candidate.TrimEnd('/'),
                bootstrapUrl.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase));
    }
}
