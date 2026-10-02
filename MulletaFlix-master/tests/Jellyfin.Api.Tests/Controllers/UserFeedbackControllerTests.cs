using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Nebula;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Models.UserFeedbackDtos;
using MulletaFlix.Database.Implementations.Entities;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class UserFeedbackControllerTests
{
    [Fact]
    public void GetMediaSuggestions_RequiresAtLeastTwoCharactersAndUsesNebulaCatalog()
    {
        var nebulaManager = new Mock<INebulaFtpManager>();
        var expected = new[] { new MediaBrowser.Model.Nebula.NebulaMediaSuggestionDto { Title = "The Example", MediaType = "Series", Year = 2025 } };
        nebulaManager.Setup(manager => manager.SearchMediaSuggestions("Exam", 7)).Returns(expected);
        var controller = new UserFeedbackController(Mock.Of<IActivityManager>(), Mock.Of<ILibraryManager>(), nebulaManager.Object);

        var emptyResult = Assert.IsType<OkObjectResult>(controller.GetMediaSuggestions("x", 7));
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyList<MediaBrowser.Model.Nebula.NebulaMediaSuggestionDto>>(emptyResult.Value));
        var result = Assert.IsType<OkObjectResult>(controller.GetMediaSuggestions("Exam", 7));

        Assert.Same(expected, result.Value);
        nebulaManager.Verify(manager => manager.SearchMediaSuggestions("Exam", 7), Times.Once);
    }

    [Fact]
    public void GetMediaSuggestionIndexStatus_ReturnsPathFreeIndexStatus()
    {
        var expected = new NebulaMediaSuggestionIndexStatusDto
        {
            State = "Indexing",
            IsIndexing = true,
            RootCount = 3,
            IndexedTitleCount = 42
        };
        var nebulaManager = new Mock<INebulaFtpManager>();
        nebulaManager.Setup(manager => manager.GetMediaSuggestionIndexStatus()).Returns(expected);
        var controller = new UserFeedbackController(Mock.Of<IActivityManager>(), Mock.Of<ILibraryManager>(), nebulaManager.Object);

        var result = Assert.IsType<OkObjectResult>(controller.GetMediaSuggestionIndexStatus());

        Assert.Same(expected, result.Value);
        nebulaManager.Verify(manager => manager.GetMediaSuggestionIndexStatus(), Times.Once);
    }

    [Fact]
    public void GetMediaRequestCatalog_ReturnsIndexedTitles()
    {
        var expected = new[] { new MediaBrowser.Model.Nebula.NebulaMediaSuggestionDto { Title = "Included Title", MediaType = "Series" } };
        var nebulaManager = new Mock<INebulaFtpManager>();
        nebulaManager.Setup(manager => manager.GetMediaSuggestionCatalog()).Returns(expected);
        var controller = new UserFeedbackController(Mock.Of<IActivityManager>(), Mock.Of<ILibraryManager>(), nebulaManager.Object);

        var result = Assert.IsType<OkObjectResult>(controller.GetMediaRequestCatalog());

        Assert.Same(expected, result.Value);
        nebulaManager.Verify(manager => manager.GetMediaSuggestionCatalog(), Times.Once);
    }

    [Fact]
    public async Task CreateMediaRequest_TrimsInputAndStoresRequest()
    {
        var activityManager = new Mock<IActivityManager>();
        var nebulaManager = new Mock<INebulaFtpManager>();
        ActivityLog? storedEntry = null;
        activityManager.Setup(manager => manager.CreateAsync(It.IsAny<ActivityLog>()))
            .Callback<ActivityLog>(entry => storedEntry = entry)
            .Returns(Task.CompletedTask);
        var controller = new UserFeedbackController(activityManager.Object, Mock.Of<ILibraryManager>(), nebulaManager.Object);
        var callerUserId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(InternalClaimTypes.UserId, callerUserId.ToString("D"))],
                    "test"))
            }
        };

        var result = await controller.CreateMediaRequest(new MediaRequestDto
        {
            Title = "  The Example  ",
            MediaType = " Series ",
            Year = 2025,
            Notes = "  Please add season two.  "
        });

        Assert.IsType<NoContentResult>(result);
        Assert.NotNull(storedEntry);
        Assert.Equal("Solicitação de mídia: The Example", storedEntry.Name);
        Assert.Equal("Series · 2025", storedEntry.Overview);
        Assert.Equal("Please add season two.", storedEntry.ShortOverview);
        Assert.Equal("MediaRequest", storedEntry.Type);
        Assert.Equal(callerUserId, storedEntry.UserId);
        nebulaManager.Verify(manager => manager.PrioritizeMedia(string.Empty, null, "The Example"), Times.Once);
    }

    [Fact]
    public async Task CreatePlaybackIssue_ReturnsNotFoundForItemOutsideAccessibleLibrary()
    {
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns((BaseItem?)null);
        var activityManager = new Mock<IActivityManager>();
        var controller = new UserFeedbackController(activityManager.Object, libraryManager.Object, Mock.Of<INebulaFtpManager>());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.CreatePlaybackIssue(new PlaybackIssueDto
        {
            ItemId = Guid.NewGuid(),
            Category = "Playback",
            Description = "The video stops."
        });

        Assert.IsType<NotFoundResult>(result);
        activityManager.Verify(manager => manager.CreateAsync(It.IsAny<ActivityLog>()), Times.Never);
    }

    [Fact]
    public async Task CreatePlaybackIssue_UsesCallerIdentityForLookupAndReport()
    {
        var callerUserId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var item = new Mock<BaseItem>();
        item.Setup(libraryItem => libraryItem.Name).Returns("The Example");
        ActivityLog? storedEntry = null;
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetItemById<BaseItem>(itemId, callerUserId)).Returns(item.Object);
        var activityManager = new Mock<IActivityManager>();
        activityManager.Setup(manager => manager.CreateAsync(It.IsAny<ActivityLog>()))
            .Callback<ActivityLog>(entry => storedEntry = entry)
            .Returns(Task.CompletedTask);
        var controller = new UserFeedbackController(activityManager.Object, libraryManager.Object, Mock.Of<INebulaFtpManager>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(InternalClaimTypes.UserId, callerUserId.ToString("D"))],
                        "test"))
                }
            }
        };

        var result = await controller.CreatePlaybackIssue(new PlaybackIssueDto
        {
            ItemId = itemId,
            Category = "Playback",
            Description = "The video stops."
        });

        Assert.IsType<NoContentResult>(result);
        libraryManager.Verify(manager => manager.GetItemById<BaseItem>(itemId, callerUserId), Times.Once);
        Assert.NotNull(storedEntry);
        Assert.Equal(callerUserId, storedEntry!.UserId);
        Assert.Equal("PlaybackIssue", storedEntry.Type);
        activityManager.Verify(manager => manager.CreateAsync(It.IsAny<ActivityLog>()), Times.Once);
    }

    [Fact]
    public async Task GetMyMediaRequests_FiltersByCallerUserIdAndMediaRequestType()
    {
        MulletaFlix.Data.Queries.ActivityLogQuery? capturedQuery = null;
        var entry = new ActivityLogEntry("Solicitação de mídia: The Example", "MediaRequest", Guid.NewGuid()) { Id = 42 };
        var expected = new MediaBrowser.Model.Querying.QueryResult<ActivityLogEntry>(0, 1, new[]
        {
            entry
        });
        var activityManager = new Mock<IActivityManager>();
        activityManager.Setup(manager => manager.GetPagedResultAsync(It.IsAny<MulletaFlix.Data.Queries.ActivityLogQuery>()))
            .Callback<MulletaFlix.Data.Queries.ActivityLogQuery>(query => capturedQuery = query)
            .ReturnsAsync(expected);
        var nebulaManager = new Mock<INebulaFtpManager>();
        nebulaManager.Setup(manager => manager.IsMediaRequestPrioritized("The Example")).Returns(true);
        var controller = new UserFeedbackController(activityManager.Object, Mock.Of<ILibraryManager>(), nebulaManager.Object);
        var callerUserId = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(InternalClaimTypes.UserId, callerUserId.ToString("D"))],
                    "test"))
            }
        };

        var result = await controller.GetMyMediaRequests(limit: 50);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var actual = Assert.IsType<MediaRequestQueryResultDto>(okResult.Value);
        Assert.Equal(new[] { 42L }, actual.PriorityRequestIds);
        Assert.Same(entry, Assert.Single(actual.Items));
        Assert.NotNull(capturedQuery);
        Assert.Equal(callerUserId, capturedQuery!.UserId);
        Assert.Equal("MediaRequest", capturedQuery!.Type);
        Assert.Equal(50, capturedQuery.Limit);
        nebulaManager.Verify(manager => manager.IsMediaRequestPrioritized("The Example"), Times.Once);
    }

    [Fact]
    public async Task GetMyMediaRequests_ReturnsQueueStatusesForOnlyTheCurrentPage()
    {
        IReadOnlyList<NebulaMediaRequestQueueQueryDto>? capturedQueueQueries = null;
        var entry = new ActivityLogEntry("Solicitação de mídia: Atomic (2025)", "MediaRequest", Guid.NewGuid())
        {
            Id = 42,
            Overview = "Series · 2025"
        };
        var activityManager = new Mock<IActivityManager>();
        activityManager.Setup(manager => manager.GetPagedResultAsync(It.IsAny<MulletaFlix.Data.Queries.ActivityLogQuery>()))
            .ReturnsAsync(new MediaBrowser.Model.Querying.QueryResult<ActivityLogEntry>(0, 1, new[] { entry }));
        var expectedStatus = new NebulaMediaRequestQueueStatusDto { RequestId = 42 };
        var nebulaManager = new Mock<INebulaFtpManager>();
        nebulaManager.Setup(manager => manager.GetMediaSuggestionCatalog()).Returns(Array.Empty<NebulaMediaSuggestionDto>());
        nebulaManager.Setup(manager => manager.GetMediaRequestQueueStatuses(It.IsAny<IReadOnlyList<NebulaMediaRequestQueueQueryDto>>()))
            .Callback<IReadOnlyList<NebulaMediaRequestQueueQueryDto>>(queries => capturedQueueQueries = queries)
            .Returns(new[] { expectedStatus });
        var controller = new UserFeedbackController(activityManager.Object, Mock.Of<ILibraryManager>(), nebulaManager.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = Assert.IsType<OkObjectResult>(await controller.GetMyMediaRequests());
        var response = Assert.IsType<MediaRequestQueryResultDto>(result.Value);

        Assert.Same(expectedStatus, Assert.Single(response.QueueStatuses));
        var query = Assert.Single(capturedQueueQueries!);
        Assert.Equal(42, query.RequestId);
        Assert.Equal("Atomic (2025)", query.Title);
        Assert.Equal("Series", query.MediaType);
        Assert.Equal(2025, query.Year);
    }

    [Theory]
    [InlineData("Solicitação de mídia: Átomic (2024)", "Atomic")]
    [InlineData("Solicitação de mídia: D&D - Cityscape", "D&D Cityscape")]
    public async Task GetMyMediaRequests_ReturnsOnlyCallerPageCatalogCandidates(string requestName, string catalogTitle)
    {
        var entry = new ActivityLogEntry(requestName, "MediaRequest", Guid.NewGuid()) { Id = 42 };
        var activityManager = new Mock<IActivityManager>();
        activityManager.Setup(manager => manager.GetPagedResultAsync(It.IsAny<MulletaFlix.Data.Queries.ActivityLogQuery>()))
            .ReturnsAsync(new MediaBrowser.Model.Querying.QueryResult<ActivityLogEntry>(0, 1, new[] { entry }));
        var nebulaManager = new Mock<INebulaFtpManager>();
        nebulaManager.Setup(manager => manager.GetMediaSuggestionCatalog()).Returns(new[]
        {
            new NebulaMediaSuggestionDto { Title = catalogTitle, MediaType = "Series", Year = 2024 },
            new NebulaMediaSuggestionDto { Title = catalogTitle, MediaType = "Animações", Year = 2025 },
            new NebulaMediaSuggestionDto { Title = "Unrequested title", MediaType = "Series" }
        });
        var controller = new UserFeedbackController(activityManager.Object, Mock.Of<ILibraryManager>(), nebulaManager.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = Assert.IsType<OkObjectResult>(await controller.GetMyMediaRequests());
        var response = Assert.IsType<MediaRequestQueryResultDto>(result.Value);

        Assert.Equal(2, response.Catalog.Count);
        Assert.All(response.Catalog, item => Assert.Equal(catalogTitle, item.Title));
    }

    [Theory]
    [InlineData(200, 200)]
    [InlineData(-10, 0)]
    public async Task GetMyMediaRequests_ClampsOutOfRangeLimitAndSupportsOffsets(int startIndex, int expectedIndex)
    {
        MulletaFlix.Data.Queries.ActivityLogQuery? capturedQuery = null;
        var activityManager = new Mock<IActivityManager>();
        activityManager.Setup(manager => manager.GetPagedResultAsync(It.IsAny<MulletaFlix.Data.Queries.ActivityLogQuery>()))
            .Callback<MulletaFlix.Data.Queries.ActivityLogQuery>(query => capturedQuery = query)
            .ReturnsAsync(new MediaBrowser.Model.Querying.QueryResult<ActivityLogEntry>(0, 0, Array.Empty<ActivityLogEntry>()));
        var controller = new UserFeedbackController(activityManager.Object, Mock.Of<ILibraryManager>(), Mock.Of<INebulaFtpManager>());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        await controller.GetMyMediaRequests(limit: 10_000, startIndex: startIndex);

        Assert.NotNull(capturedQuery);
        Assert.Equal(500, capturedQuery!.Limit);
        Assert.Equal(expectedIndex, capturedQuery.Skip);
        Assert.Equal("MediaRequest", capturedQuery.Type);
    }
}
