using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaCompletedMediaRefreshTests
{
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
}
