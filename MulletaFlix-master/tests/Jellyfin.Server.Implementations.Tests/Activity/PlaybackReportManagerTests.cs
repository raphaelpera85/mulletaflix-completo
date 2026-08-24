using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MulletaFlix.Data.Queries;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Enums;
using MulletaFlix.Server.Implementations.Activity;

namespace MulletaFlix.Server.Implementations.Tests.Activity;

/// <summary>
/// Tests for PlaybackReportManager.
/// </summary>
public class PlaybackReportManagerTests
{
    private readonly DbContextOptions<UsersDbContext> _options;
    private readonly Mock<ILogger<PlaybackReportManager>> _loggerMock;
    private readonly Mock<ILogger<UsersDbContext>> _contextLoggerMock;

    public PlaybackReportManagerTests()
    {
        _options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _loggerMock = new Mock<ILogger<PlaybackReportManager>>();
        _contextLoggerMock = new Mock<ILogger<UsersDbContext>>();
    }

    private UsersDbContext CreateContext() => new(_options, _contextLoggerMock.Object);

    private PlaybackReportManager CreateManager(UsersDbContext context)
    {
        var factoryMock = new Mock<IDbContextFactory<UsersDbContext>>();
        factoryMock.Setup(f => f.CreateDbContextAsync(default))
            .ReturnsAsync(context);
        return new PlaybackReportManager(factoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldAddReportToDatabase()
    {
        // Arrange
        await using var context = CreateContext();
        var manager = CreateManager(context);

        var report = new PlaybackReport(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Movie",
            "Movie",
            "device1",
            "Device Name",
            "Client Name",
            "session1",
            "sessionId")
        {
            StartTimeUtc = DateTime.UtcNow,
            DurationSeconds = 3600,
            PlayedToCompletion = true
        };

        // Act
        await manager.CreateAsync(report);

        // Assert - use a new context to verify since the manager disposes the context
        await using var verifyContext = CreateContext();
        var saved = await verifyContext.PlaybackReports.FirstOrDefaultAsync(r => r.Id == report.Id);
        Assert.NotNull(saved);
        Assert.Equal("Test Movie", saved!.ItemName);
        Assert.Equal(3600, saved.DurationSeconds);
        Assert.True(saved.PlayedToCompletion);
    }

    [Fact]
    public async Task GetPagedResultAsync_ShouldFilterByUserId()
    {
        // Arrange
        await using var context = CreateContext();
        var manager = CreateManager(context);

        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();

        var report1 = new PlaybackReport(userId1, Guid.NewGuid(), "Movie 1", "Movie", "d1", "Device", "Client", "s1", "sid");
        var report2 = new PlaybackReport(userId2, Guid.NewGuid(), "Movie 2", "Movie", "d2", "Device", "Client", "s2", "sid");

        context.PlaybackReports.AddRange(report1, report2);
        await context.SaveChangesAsync();

        // Act
        var query = new PlaybackReportQuery { UserId = userId1, Limit = 10 };
        var result = await manager.GetPagedResultAsync(query);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(userId1, result.Items[0].UserId);
    }

    [Fact]
    public async Task GetPagedResultAsync_ShouldFilterByItemId()
    {
        // Arrange
        await using var context = CreateContext();
        var manager = CreateManager(context);

        var itemId1 = Guid.NewGuid();
        var itemId2 = Guid.NewGuid();

        var report1 = new PlaybackReport(Guid.NewGuid(), itemId1, "Movie 1", "Movie", "d1", "Device", "Client", "s1", "sid");
        var report2 = new PlaybackReport(Guid.NewGuid(), itemId2, "Movie 2", "Movie", "d2", "Device", "Client", "s2", "sid");

        context.PlaybackReports.AddRange(report1, report2);
        await context.SaveChangesAsync();

        // Act
        var query = new PlaybackReportQuery { ItemId = itemId1, Limit = 10 };
        var result = await manager.GetPagedResultAsync(query);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(itemId1, result.Items[0].ItemId);
    }

    [Fact]
    public async Task GetPagedResultAsync_ShouldFilterByPlayedToCompletion()
    {
        // Arrange
        await using var context = CreateContext();
        var manager = CreateManager(context);

        var report1 = new PlaybackReport(Guid.NewGuid(), Guid.NewGuid(), "Movie 1", "Movie", "d1", "Device", "Client", "s1", "sid")
        {
            PlayedToCompletion = true
        };
        var report2 = new PlaybackReport(Guid.NewGuid(), Guid.NewGuid(), "Movie 2", "Movie", "d2", "Device", "Client", "s2", "sid")
        {
            PlayedToCompletion = false
        };

        context.PlaybackReports.AddRange(report1, report2);
        await context.SaveChangesAsync();

        // Act
        var query = new PlaybackReportQuery { PlayedToCompletion = true, Limit = 10 };
        var result = await manager.GetPagedResultAsync(query);

        // Assert
        Assert.Single(result.Items);
        Assert.True(result.Items[0].PlayedToCompletion);
    }

    [Fact]
    public async Task GetPagedResultAsync_ShouldOrderByDateCreatedDescending()
    {
        // Arrange
        await using var context = CreateContext();
        var manager = CreateManager(context);

        var now = DateTime.UtcNow;
        var report1 = new PlaybackReport(Guid.NewGuid(), Guid.NewGuid(), "Movie 1", "Movie", "d1", "Device", "Client", "s1", "sid")
        {
            DateCreated = now.AddHours(-2)
        };
        var report2 = new PlaybackReport(Guid.NewGuid(), Guid.NewGuid(), "Movie 2", "Movie", "d2", "Device", "Client", "s2", "sid")
        {
            DateCreated = now.AddHours(-1)
        };

        context.PlaybackReports.AddRange(report1, report2);
        await context.SaveChangesAsync();

        // Act
        var query = new PlaybackReportQuery
        {
            Limit = 10,
            OrderBy = new[] { (PlaybackReportSortBy.DateCreated, SortOrder.Descending) }
        };
        var result = await manager.GetPagedResultAsync(query);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.True(result.Items[0].DateCreated > result.Items[1].DateCreated);
    }

    [Fact]
    public async Task GetStatsAsync_ShouldReturnAggregatedStatistics()
    {
        // Arrange
        await using var context = CreateContext();
        var manager = CreateManager(context);

        var userId = Guid.NewGuid();
        var report1 = new PlaybackReport(userId, Guid.NewGuid(), "Movie 1", "Movie", "d1", "Device", "Client", "s1", "sid")
        {
            DurationSeconds = 3600,
            PlayedToCompletion = true,
            PlayMethod = "DirectPlay",
            ItemType = "Movie"
        };
        var report2 = new PlaybackReport(userId, Guid.NewGuid(), "Movie 2", "Movie", "d2", "Device", "Client", "s2", "sid")
        {
            DurationSeconds = 1800,
            PlayedToCompletion = false,
            PlayMethod = "Transcode",
            WasTranscoded = true,
            ItemType = "Movie"
        };
        var report3 = new PlaybackReport(userId, Guid.NewGuid(), "Song 1", "Audio", "d3", "Device", "Client", "s3", "sid")
        {
            DurationSeconds = 300,
            PlayedToCompletion = true,
            PlayMethod = "DirectPlay",
            ItemType = "Audio"
        };

        context.PlaybackReports.AddRange(report1, report2, report3);
        await context.SaveChangesAsync();

        // Act
        var query = new PlaybackReportQuery { Limit = 100 };
        var stats = await manager.GetStatsAsync(query);

        // Assert
        Assert.Equal(3, stats.TotalPlays);
        Assert.Equal(1, stats.UniqueUsers);
        Assert.Equal(3, stats.UniqueItems);
        Assert.Equal(5700, stats.TotalDurationSeconds);
        Assert.Equal(1900, stats.AverageDurationSeconds);
        Assert.Equal(2, stats.DirectPlayPlays);
        Assert.Equal(0, stats.DirectStreamPlays);
        Assert.Equal(1, stats.TranscodedPlays);
        Assert.Equal(2, stats.PlaysByItemType["Movie"]);
        Assert.Equal(1, stats.PlaysByItemType["Audio"]);
    }
}