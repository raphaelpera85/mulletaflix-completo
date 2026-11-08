using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.LiveTv;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class LiveTvControllerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResetTuner_EmptyTunerId_ReturnsBadRequest(string? tunerId)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.ResetTuner(tunerId!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetTimer_EmptyTimerId_ReturnsBadRequest(string? timerId)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetTimer(timerId!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelTimer_EmptyTimerId_ReturnsBadRequest(string? timerId)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.CancelTimer(timerId!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateTimer_EmptyTimerId_ReturnsBadRequest(string? timerId)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.UpdateTimer(timerId!, new TimerInfoDto());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateTimer_NullBody_ReturnsBadRequest()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.UpdateTimer("timer-1", null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateTimer_NullBody_ReturnsBadRequest()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.CreateTimer(null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CancelSeriesTimer_EmptyTimerId_ReturnsBadRequest(string? timerId)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.CancelSeriesTimer(timerId!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateSeriesTimer_EmptyTimerId_ReturnsBadRequest(string? timerId)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.UpdateSeriesTimer(timerId!, new SeriesTimerInfoDto());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateSeriesTimer_NullBody_ReturnsBadRequest()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.UpdateSeriesTimer("series-timer-1", null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CreateSeriesTimer_NullBody_ReturnsBadRequest()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.CreateSeriesTimer(null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddTunerHost_NullBody_ReturnsBadRequest()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.AddTunerHost(null!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeleteTunerHost_EmptyId_ReturnsBadRequest(string? id)
    {
        var fixture = CreateFixture();

        var result = fixture.Controller.DeleteTunerHost(id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AddListingProvider_NullBody_ReturnsBadRequest()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.AddListingProvider(null, null!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeleteListingProvider_EmptyId_ReturnsBadRequest(string? id)
    {
        var fixture = CreateFixture();

        var result = fixture.Controller.DeleteListingProvider(id);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetLiveRecordingFile_EmptyStreamId_ReturnsBadRequest(string? streamId)
    {
        var fixture = CreateFixture();

        var result = fixture.Controller.GetLiveRecordingFile(streamId!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetLiveStreamFile_EmptyStreamId_ReturnsBadRequest(string? streamId)
    {
        var fixture = CreateFixture();

        var result = fixture.Controller.GetLiveStreamFile(streamId!, "ts");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task GetLiveTvChannels_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetLiveTvChannels(
            null, null, startIndex, null, null, null, null, null, null, null, null, null, null, null, [], [], null, [], null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-25)]
    public async Task GetLiveTvChannels_NegativeLimit_ReturnsBadRequest(int limit)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetLiveTvChannels(
            null, null, null, null, null, null, null, null, limit, null, null, null, null, null, [], [], null, [], null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task GetLiveTvChannels_NegativeImageTypeLimit_ReturnsBadRequest(int imageTypeLimit)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetLiveTvChannels(
            null, null, null, null, null, null, null, null, null, null, null, null, null, imageTypeLimit, [], [], null, [], null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task GetRecordings_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetRecordings(
            null, null, startIndex, null, null, null, null, null, null, [], [], null, null, null, null, null, null, null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task GetLiveTvPrograms_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetLiveTvPrograms(
            [], null, null, null, null, null, null, null, null, null, null, null, null, startIndex, null, [], [], [], [], null, null, [], null, null, null, []);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetPrograms_NullBody_ReturnsBadRequest()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetPrograms(null!);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task GetPrograms_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetPrograms(new MulletaFlix.Api.Models.LiveTvDtos.GetProgramsDto
        {
            StartIndex = startIndex
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    public async Task GetRecommendedPrograms_NegativeStartIndex_ReturnsBadRequest(int startIndex)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetRecommendedPrograms(
            null, startIndex, null, null, null, null, null, null, null, null, null, null, [], [], [], null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetProgram_EmptyProgramId_ReturnsBadRequest(string? programId)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetProgram(programId!, null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeleteRecording_EmptyRecordingId_ReturnsBadRequest()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.DeleteRecording(Guid.Empty);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static (LiveTvController Controller, Mock<ILiveTvManager> LiveTvManager, Mock<ITunerHostManager> TunerHostManager, Mock<IListingsManager> ListingsManager) CreateFixture()
    {
        var liveTvManager = new Mock<ILiveTvManager>(MockBehavior.Loose);
        var guideManager = new Mock<IGuideManager>(MockBehavior.Loose);
        var tunerHostManager = new Mock<ITunerHostManager>(MockBehavior.Loose);
        var listingsManager = new Mock<IListingsManager>(MockBehavior.Loose);
        var recordingsManager = new Mock<IRecordingsManager>(MockBehavior.Loose);
        var userManager = new Mock<IUserManager>(MockBehavior.Loose);
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Loose);
        var dtoService = new Mock<IDtoService>(MockBehavior.Loose);
        var mediaSourceManager = new Mock<IMediaSourceManager>(MockBehavior.Loose);
        var transcodeManager = new Mock<ITranscodeManager>(MockBehavior.Loose);
        var schedulesDirect = new Mock<ISchedulesDirectService>(MockBehavior.Loose);

        var userId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Role, UserRoles.Administrator)
                ],
                "Custom"))
        };

        var controller = new LiveTvController(
            liveTvManager.Object,
            guideManager.Object,
            tunerHostManager.Object,
            listingsManager.Object,
            recordingsManager.Object,
            userManager.Object,
            libraryManager.Object,
            dtoService.Object,
            mediaSourceManager.Object,
            transcodeManager.Object,
            schedulesDirect.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        return (controller, liveTvManager, tunerHostManager, listingsManager);
    }
}
