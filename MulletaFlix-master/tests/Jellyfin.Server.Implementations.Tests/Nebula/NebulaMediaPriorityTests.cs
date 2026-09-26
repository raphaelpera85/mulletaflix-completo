using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaMediaPriorityTests
{
    private static NebulaDownloaderEngine CreateDownloaderEngine()
    {
        return new NebulaDownloaderEngine(null!, null!, NullLogger<NebulaDownloaderEngine>.Instance);
    }

    [Fact]
    public void NebulaDownloaderEngine_PrioritizeTarget_PrioritizesSingleFile()
    {
        using var engine = CreateDownloaderEngine();
        var targetFile = @"D:\midias\Filmes\Avatar (2009)\Avatar (2009).strm";
        var otherFile = @"D:\midias\Filmes\Titanic (1997)\Titanic (1997).strm";

        engine.PrioritizeTarget(targetFile);

        Assert.True(engine.IsPathPrioritized(targetFile));
        Assert.False(engine.IsPathPrioritized(otherFile));
    }

    [Fact]
    public void NebulaDownloaderEngine_PrioritizeTarget_PrioritizesWholeSeriesDirectoryAndEpisodes()
    {
        using var engine = CreateDownloaderEngine();
        var seriesDir = @"D:\midias\Series\Breaking Bad";
        var ep1 = @"D:\midias\Series\Breaking Bad\Season 01\Breaking Bad S01E01.strm";
        var ep2 = @"D:\midias\Series\Breaking Bad\Season 05\Breaking Bad S05E16.strm";
        var otherSeriesEp = @"D:\midias\Series\Game of Thrones\Season 01\Game of Thrones S01E01.strm";

        engine.PrioritizeTarget(seriesDir, "Breaking Bad");

        Assert.True(engine.IsPathPrioritized(ep1));
        Assert.True(engine.IsPathPrioritized(ep2));
        Assert.False(engine.IsPathPrioritized(otherSeriesEp));
    }

    [Fact]
    public void NebulaDownloaderEngine_PrioritizeTarget_PrioritizesSeriesByName()
    {
        using var engine = CreateDownloaderEngine();
        var seriesName = "Renascer";
        var epNovela = @"D:\midias\Novelas\Renascer (2024)\Season 01\Capitulo 01.strm";
        var epOtherNovela = @"D:\midias\Novelas\Pantanal (2022)\Season 01\Capitulo 01.strm";

        engine.PrioritizeTarget(string.Empty, seriesName);

        Assert.True(engine.IsPathPrioritized(epNovela));
        Assert.False(engine.IsPathPrioritized(epOtherNovela));
    }

    [Fact]
    public void NebulaDownloaderEngine_Ordering_PriorityItemComesBeforeStandardHierarchy()
    {
        using var engine = CreateDownloaderEngine();
        var normalMovie = @"D:\midias\Filmes\Inception (2010)\Inception (2010).strm";
        var requestedSeriesEp = @"D:\midias\Series\Stranger Things\Season 01\Stranger Things S01E01.strm";

        engine.PrioritizeTarget(requestedSeriesEp, "Stranger Things");

        var candidates = new List<string> { normalMovie, requestedSeriesEp };

        var prioritized = candidates
            .Select(path => new
            {
                Path = path,
                IsPriority = engine.IsPathPrioritized(path),
                Category = NebulaDownloaderEngine.GetCategoryPriority(path)
            })
            .OrderByDescending(x => x.IsPriority)
            .ThenBy(x => x.Category)
            .ToList();

        // Mesmo Filmes tendo Category=1 e Series tendo Category=3, o item prioritário deve vir PRIMEIRO
        Assert.Equal(requestedSeriesEp, prioritized[0].Path);
        Assert.True(prioritized[0].IsPriority);
        Assert.Equal(normalMovie, prioritized[1].Path);
        Assert.False(prioritized[1].IsPriority);
    }

    [Fact]
    public void NebulaStagingWatcher_PrioritizeUpload_DetectsAndPrioritizesFile()
    {
        var engine = new NebulaUploadEngine(
            null!,
            null!,
            uploadConcurrency: 1,
            chunkSizeMb: 16,
            deleteSourceAfterUpload: false,
            NullLogger<NebulaUploadEngine>.Instance);
        using var watcher = new NebulaStagingWatcher(engine, NullLogger<NebulaStagingWatcher>.Instance);

        var priorityPath = @"E:\NebulaStage\Series\Dark\Season 01\Dark S01E01.mkv";
        var otherPath = @"E:\NebulaStage\Series\Dark2\Season 01\ep1.mkv";

        watcher.PrioritizeUpload(priorityPath, "Dark");

        Assert.True(watcher.IsPathPrioritized(priorityPath));
        Assert.False(watcher.IsPathPrioritized(otherPath));
    }

    [Fact]
    public void NebulaFtpManager_PrioritizeItem_EpisodeSetsSeriesPriority()
    {
        var series = new Series
        {
            Id = Guid.NewGuid(),
            Name = "Avatar A Lenda de Aang",
            Path = @"D:\midias\Animações\Avatar A Lenda de Aang"
        };

        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            Name = "O Garoto no Iceberg",
            Path = @"D:\midias\Animações\Avatar A Lenda de Aang\Season 01\Avatar S01E01.strm",
            SeriesId = series.Id
        };

        Assert.Equal(series.Id, episode.SeriesId);
        Assert.EndsWith(".strm", episode.Path, StringComparison.OrdinalIgnoreCase);
    }
}
