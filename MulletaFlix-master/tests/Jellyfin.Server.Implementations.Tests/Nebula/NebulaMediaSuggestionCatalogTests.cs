using System;
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
}
