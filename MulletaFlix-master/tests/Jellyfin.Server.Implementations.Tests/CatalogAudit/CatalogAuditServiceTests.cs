using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.CatalogAudit;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Server.Implementations.CatalogAudit;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.CatalogAudit;

public class CatalogAuditServiceTests
{
    private readonly Mock<ILibraryManager> _libraryManagerMock = new();

    private CatalogAuditService CreateService()
    {
        return new CatalogAuditService(_libraryManagerMock.Object, NullLogger<CatalogAuditService>.Instance);
    }

    [Fact]
    public void AnalyzeItem_YearMismatch_DetectsInconsistency()
    {
        var service = CreateService();
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Name = "The Matrix",
            Path = "C:\\Media\\Movies\\The.Matrix.1999.1080p.mkv",
            ProductionYear = 2003
        };

        var inconsistencies = service.AnalyzeItem(movie);

        Assert.Contains(inconsistencies, inc => inc.Category == CatalogInconsistencyCategory.YearMismatch
            && inc.Field == "ProductionYear"
            && inc.CurrentValue == "2003"
            && inc.ExpectedValue == "1999");
    }

    [Fact]
    public void AnalyzeItem_MissingProviderIds_DetectsMissingMovieAndBookIdentifiers()
    {
        var service = CreateService();
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Name = "Unknown Movie",
            Path = "C:\\Media\\Movies\\Unknown.Movie.2020.mkv",
            ProductionYear = 2020
        };

        var movieInc = service.AnalyzeItem(movie);
        Assert.Contains(movieInc, inc => inc.Category == CatalogInconsistencyCategory.MissingProviderId
            && inc.Field == "ProviderIds");

        var book = new Book
        {
            Id = Guid.NewGuid(),
            Name = "Clean Code",
            Path = "C:\\Media\\Books\\Clean.Code.epub"
        };

        var bookInc = service.AnalyzeItem(book);
        Assert.Contains(bookInc, inc => inc.Category == CatalogInconsistencyCategory.MissingProviderId
            && inc.ExpectedValue == "OpenLibrary or Isbn");
    }

    [Fact]
    public void AnalyzeItem_MediaTypeMismatch_DetectsVideoFileUnderBookType()
    {
        var service = CreateService();
        var book = new Book
        {
            Id = Guid.NewGuid(),
            Name = "Misclassified Video",
            Path = "C:\\Media\\Books\\VideoLecture.mkv"
        };

        var inconsistencies = service.AnalyzeItem(book);

        Assert.Contains(inconsistencies, inc => inc.Category == CatalogInconsistencyCategory.MediaTypeMismatch
            && inc.Severity == InconsistencySeverity.Error);
    }

    [Fact]
    public void AnalyzeItem_LockedFields_PreservesProvenanceNotice()
    {
        var service = CreateService();
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Name = "Locked Title",
            Path = "C:\\Media\\Movies\\Locked.Title.2021.mkv",
            ProductionYear = 2021,
            LockedFields = [MetadataField.Name, MetadataField.ProductionLocations]
        };

        var inconsistencies = service.AnalyzeItem(movie);

        Assert.Contains(inconsistencies, inc => inc.Category == CatalogInconsistencyCategory.LockedFieldConflict
            && inc.Field == "LockedFields");
    }

    [Fact]
    public async Task ApplyFixAsync_AndRollback_UpdatesAndRestoresStateSafely()
    {
        var service = CreateService();
        var itemId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = itemId,
            Name = "The Matrix",
            Path = "C:\\Media\\Movies\\The.Matrix.1999.1080p.mkv",
            ProductionYear = 2003
        };

        _libraryManagerMock.Setup(m => m.GetItemById(itemId)).Returns(movie);

        // Apply fix to correct year to 1999
        var fixRequest = new CatalogAuditFixRequest
        {
            ItemId = itemId,
            Field = "ProductionYear",
            ApprovedValue = "1999",
            PreserveLockedFields = true
        };

        var history = await service.ApplyFixAsync(fixRequest, "adminUser");

        Assert.Equal("2003", history.PreviousValue);
        Assert.Equal("1999", history.AppliedValue);
        Assert.Equal(1999, movie.ProductionYear);

        // Rollback fix
        var rollbackSuccess = await service.RollbackFixAsync(new CatalogAuditRollbackRequest
        {
            HistoryEntryId = history.Id
        });

        Assert.True(rollbackSuccess);
        Assert.Equal(2003, movie.ProductionYear);
    }

    [Fact]
    public async Task ApplyFixAsync_LockedItemWithPreserveLockedFields_ThrowsInvalidOperationException()
    {
        var service = CreateService();
        var itemId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = itemId,
            Name = "The Matrix",
            Path = "C:\\Media\\Movies\\The.Matrix.1999.1080p.mkv",
            ProductionYear = 2003,
            IsLocked = true
        };

        _libraryManagerMock.Setup(m => m.GetItemById(itemId)).Returns(movie);

        var fixRequest = new CatalogAuditFixRequest
        {
            ItemId = itemId,
            Field = "Name",
            ApprovedValue = "The Matrix (Remastered)",
            PreserveLockedFields = true
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyFixAsync(fixRequest, "adminUser"));
    }

    [Fact]
    public void GetProviderTerms_ReturnsConfiguredProvidersAndLimits()
    {
        var service = CreateService();
        var terms = service.GetProviderTerms();

        Assert.Contains(terms, t => t.ProviderName == "OpenLibrary" && !t.RequiresApiKey);
        Assert.Contains(terms, t => t.ProviderName.Contains("TMDB", StringComparison.OrdinalIgnoreCase) && t.RequiresApiKey);
    }
}
