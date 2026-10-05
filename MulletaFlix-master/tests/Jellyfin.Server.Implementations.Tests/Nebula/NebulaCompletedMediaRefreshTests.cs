using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using MongoDB.Bson;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaCompletedMediaRefreshTests
{
    [Fact]
    public void GetCompletedSince_OverlapsRefreshIntervalToAvoidMissingRecentCompletions()
    {
        var now = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

        var completedSince = Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.GetCompletedSince(now);

        Assert.Equal(new DateTimeOffset(2026, 10, 5, 11, 30, 0, TimeSpan.Zero), completedSince);
    }

    [Fact]
    public async Task RunImmediatelyThenPeriodicallyAsync_RefreshesBeforeWaitingForFirstInterval()
    {
        var refreshCount = 0;

        await Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.RunImmediatelyThenPeriodicallyAsync(
            _ =>
            {
                refreshCount++;
                return Task.CompletedTask;
            },
            _ => ValueTask.FromResult(false),
            CancellationToken.None);

        Assert.Equal(1, refreshCount);
    }

    [Fact]
    public async Task RunImmediatelyThenPeriodicallyAsync_RefreshesAgainAfterEachIntervalTick()
    {
        var refreshCount = 0;
        var tickCount = 0;

        await Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.RunImmediatelyThenPeriodicallyAsync(
            _ =>
            {
                refreshCount++;
                return Task.CompletedTask;
            },
            _ => ValueTask.FromResult(++tickCount == 1),
            CancellationToken.None);

        Assert.Equal(2, refreshCount);
        Assert.Equal(2, tickCount);
    }

    [Fact]
    public void GetVirtualDirectories_UsesCanonicalCategoryAndDeduplicatesDirectories()
    {
        var parentId = ObjectId.GenerateNewId().ToString();
        var files = new[]
        {
            new BsonDocument { ["name"] = "Atomic S01E01.mkv", ["parent"] = parentId },
            new BsonDocument { ["name"] = "Atomic S01E02.mkv", ["parent"] = parentId }
        };

        var directories = Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.GetVirtualDirectories(
            files,
            new Dictionary<string, string> { [parentId] = "Animações/Atomic" });

        Assert.Equal(new[] { "/Animações/Atomic" }, directories);
    }

    [Fact]
    public void GetVirtualDirectories_SkipsRecordsWithUnresolvableParent()
    {
        var directories = Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.GetVirtualDirectories(
            [new BsonDocument { ["name"] = "A Série S01E01.mkv", ["parent"] = ObjectId.GenerateNewId() }],
            new Dictionary<string, string>());

        Assert.Empty(directories);
    }

    [Fact]
    public void GetVirtualDirectories_ResolvesLegacyCanonicalStringParent()
    {
        var directories = Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.GetVirtualDirectories(
            [new BsonDocument { ["name"] = "The Show S01E01.mkv", ["parent"] = "/raphael/Series/The Show" }],
            new Dictionary<string, string>());

        Assert.Equal("/Series/The Show", Assert.Single(directories));
    }

    [Fact]
    public void GetVirtualDirectories_RoutesRootMediaByFilename()
    {
        var directories = Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.GetVirtualDirectories(
            [new BsonDocument { ["name"] = "Filme (2026).mkv", ["parent"] = BsonNull.Value }],
            new Dictionary<string, string>());

        Assert.Equal("/Filmes", Assert.Single(directories));
    }

    [Theory]
    [InlineData("{\"result\":{\"/Series/Nova\":\"file does not exist\"}}", "/Series/Nova", "file does not exist")]
    [InlineData("{\"error\":\"refresh failed\"}", "/Series/Nova", "refresh failed")]
    [InlineData("{}", "/Series/Nova", "rclone não retornou o mapa result esperado")]
    [InlineData("{\"result\":{}}", "/Series/Nova", "rclone não retornou resultado para o diretório /Series/Nova")]
    public void GetRcloneRefreshError_DetectsTopLevelPerDirectoryAndMalformedResponses(string json, string directory, string expected)
    {
        using var document = JsonDocument.Parse(json);

        var error = Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.GetRcloneRefreshError(document.RootElement, directory);

        Assert.Equal(expected, error);
    }

    [Theory]
    [InlineData("{\"result\":{\"/Series/Nova\":\"OK\"}}", "/Series/Nova")]
    [InlineData("{\"result\":{\"/\":\"OK\"}}", "/")]
    public void GetRcloneRefreshError_ReturnsNullForSuccessfulDirectoryRefresh(string json, string directory)
    {
        using var document = JsonDocument.Parse(json);

        var error = Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.GetRcloneRefreshError(document.RootElement, directory);

        Assert.Null(error);
    }

    [Fact]
    public void GetRefreshAncestors_ReturnsRootAndEveryDirectoryInOrder()
    {
        var ancestors = Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.GetRefreshAncestors("/Series/Nova/Season 1");

        Assert.Equal(new[] { "/", "/Series", "/Series/Nova", "/Series/Nova/Season 1" }, ancestors);
    }

    [Fact]
    public void GetRefreshAncestors_IgnoresPathsOutsideVisibleMediaRoots()
    {
        var ancestors = Jellyfin.Server.Implementations.Nebula.NebulaCompletedMediaRefresh.GetRefreshAncestors("/Interno/Nova");

        Assert.Empty(ancestors);
    }
}
