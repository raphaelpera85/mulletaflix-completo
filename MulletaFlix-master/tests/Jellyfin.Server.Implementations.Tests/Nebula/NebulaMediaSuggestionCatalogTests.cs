using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaMediaSuggestionCatalogTests
{
    [Fact]
    public async Task GetItemsReturnsImmediatelyAndPublishesBackgroundIndex()
    {
        using var scanStarted = new ManualResetEventSlim();
        using var finishScan = new ManualResetEventSlim();
        await using var catalog = new NebulaMediaSuggestionCatalog(
            () => ["library"],
            (_, cancellationToken) =>
            {
                scanStarted.Set();
                finishScan.Wait(cancellationToken);
                return new NebulaMediaSuggestionCatalogBuildResult(
                    [new NebulaMediaSuggestionDto { Title = "Indexed Series", MediaType = "Series" }],
                    0);
            },
            NullLogger.Instance);

        Assert.Empty(catalog.GetItems());
        Assert.True(scanStarted.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(catalog.GetStatus().IsIndexing);

        finishScan.Set();
        Assert.True(SpinWait.SpinUntil(() => catalog.GetStatus().State == "Ready", TimeSpan.FromSeconds(5)));
        Assert.Equal("Indexed Series", Assert.Single(catalog.GetItems()).Title);
        Assert.Equal(1, catalog.GetStatus().IndexedTitleCount);
    }

    [Fact]
    public async Task FailedRefreshPreservesLastSuccessfulCatalog()
    {
        var roots = new[] { "first" };
        var scanCount = 0;
        await using var catalog = new NebulaMediaSuggestionCatalog(
            () => roots,
            (_, _) =>
            {
                if (Interlocked.Increment(ref scanCount) > 1)
                {
                    throw new InvalidOperationException("simulated scanner failure");
                }

                return new NebulaMediaSuggestionCatalogBuildResult(
                    [new NebulaMediaSuggestionDto { Title = "Last Good", MediaType = "Series" }],
                    0);
            },
            NullLogger.Instance);

        _ = catalog.GetItems();
        Assert.True(SpinWait.SpinUntil(() => catalog.GetStatus().State == "Ready", TimeSpan.FromSeconds(5)));
        roots = ["changed-root"];

        Assert.Equal("Last Good", Assert.Single(catalog.GetItems()).Title);
        Assert.True(SpinWait.SpinUntil(() => catalog.GetStatus().State == "Error", TimeSpan.FromSeconds(5)));
        Assert.Equal("Last Good", Assert.Single(catalog.GetItems()).Title);
        Assert.Equal(2, scanCount);
    }

    [Fact]
    public async Task RefreshAsyncReindexesRootsThatBecomeAvailableWithoutChangingTheirPaths()
    {
        var scanCount = 0;
        await using var catalog = new NebulaMediaSuggestionCatalog(
            () => ["N:\\Series"],
            (_, _) => new NebulaMediaSuggestionCatalogBuildResult(
                Interlocked.Increment(ref scanCount) == 1
                    ? Array.Empty<NebulaMediaSuggestionDto>()
                    : [new NebulaMediaSuggestionDto { Title = "Mounted Series", MediaType = "Series" }],
                0),
            NullLogger.Instance);

        _ = catalog.GetItems();
        Assert.True(SpinWait.SpinUntil(() => catalog.GetStatus().State == "Ready", TimeSpan.FromSeconds(5)));

        await catalog.RefreshAsync();

        Assert.Equal("Mounted Series", Assert.Single(catalog.GetItems()).Title);
        Assert.Equal(2, scanCount);
    }

    [Fact]
    public async Task PartialIndexIsPublishedWithWarningWhenASubdirectoryIsInaccessible()
    {
        var root = Path.Combine("library", "root");
        var inaccessible = Path.Combine(root, "restricted");
        var accessible = Path.Combine(root, "accessible");

        IEnumerable<string> EnumerateFiles(string directory)
        {
            if (string.Equals(directory, inaccessible, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("simulated inaccessible directory");
            }

            return string.Equals(directory, accessible, StringComparison.OrdinalIgnoreCase)
                ? [Path.Combine(accessible, "S01E01.strm")]
                : Array.Empty<string>();
        }

        IEnumerable<string> EnumerateDirectories(string directory)
        {
            return string.Equals(directory, root, StringComparison.OrdinalIgnoreCase)
                ? [inaccessible, accessible]
                : Array.Empty<string>();
        }

        var enumeration = NebulaStrmDirectoryEnumerator.Enumerate(
            root,
            EnumerateFiles,
            EnumerateDirectories,
            CancellationToken.None);

        Assert.Equal([Path.Combine(accessible, "S01E01.strm")], enumeration.Files);
        Assert.Equal(1, enumeration.FailedDirectoryCount);

        await using var catalog = new NebulaMediaSuggestionCatalog(
            () => [root],
            (_, _) => new NebulaMediaSuggestionCatalogBuildResult(
                [new NebulaMediaSuggestionDto { Title = "Accessible Series", MediaType = "Series" }],
                enumeration.FailedDirectoryCount),
            NullLogger.Instance);

        _ = catalog.GetItems();
        Assert.True(SpinWait.SpinUntil(() => catalog.GetStatus().State == "ReadyWithWarnings", TimeSpan.FromSeconds(5)));
        Assert.Equal(1, catalog.GetStatus().FailedRootCount);
        Assert.Equal("Accessible Series", Assert.Single(catalog.GetItems()).Title);
    }
}
