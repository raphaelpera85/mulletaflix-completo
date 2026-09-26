using System;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Activity;
using Moq;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Models.UserFeedbackDtos;
using MulletaFlix.Database.Implementations.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
    public async Task CreateMediaRequest_TrimsInputAndStoresRequest()
    {
        var activityManager = new Mock<IActivityManager>();
        var nebulaManager = new Mock<INebulaFtpManager>();
        ActivityLog? storedEntry = null;
        activityManager.Setup(manager => manager.CreateAsync(It.IsAny<ActivityLog>()))
            .Callback<ActivityLog>(entry => storedEntry = entry)
            .Returns(Task.CompletedTask);
        var controller = new UserFeedbackController(activityManager.Object, Mock.Of<ILibraryManager>(), nebulaManager.Object);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

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
}
