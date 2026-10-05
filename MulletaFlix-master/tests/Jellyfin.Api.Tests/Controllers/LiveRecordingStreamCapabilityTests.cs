using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Streaming;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace Jellyfin.Api.Tests.Controllers;

public sealed class LiveRecordingStreamCapabilityTests
{
    [Fact]
    public void GetLiveRecordingFile_UsesStreamCapabilityLookup()
    {
        const string streamCapability = "opaque-stream-capability";
        var path = Path.GetTempFileName();
        var recordingsManager = new Mock<IRecordingsManager>();
        recordingsManager
            .Setup(manager => manager.GetActiveRecordingStreamPath(streamCapability))
            .Returns(path);
        var controller = CreateController(recordingsManager.Object);

        try
        {
            var result = Assert.IsType<FileStreamResult>(controller.GetLiveRecordingFile(streamCapability));
            result.FileStream.Dispose();
            Assert.Equal("no-store", controller.Response.Headers["Cache-Control"].ToString());
            recordingsManager.Verify(manager => manager.GetActiveRecordingStreamPath(streamCapability), Times.Once);
            recordingsManager.Verify(manager => manager.GetActiveRecordingPath(It.IsAny<string>()), Times.Never);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void GetLiveRecordingFile_ReturnsNotFoundWhenCapabilityDoesNotResolve()
    {
        const string timerId = "timer-id-exposed-by-timer-dto";
        var recordingsManager = new Mock<IRecordingsManager>();
        recordingsManager
            .Setup(manager => manager.GetActiveRecordingStreamPath(timerId))
            .Returns((string?)null);
        var controller = CreateController(recordingsManager.Object);

        var result = controller.GetLiveRecordingFile(timerId);

        Assert.IsType<NotFoundResult>(result);
        recordingsManager.Verify(manager => manager.GetActiveRecordingPath(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetLiveRecordingFile_RemainsAnonymousForLocalTranscoder()
    {
        var method = typeof(LiveTvController).GetMethod(nameof(LiveTvController.GetLiveRecordingFile), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        Assert.NotNull(method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).SingleOrDefault());
    }

    private static LiveTvController CreateController(IRecordingsManager recordingsManager)
    {
        var controller = new LiveTvController(
            Mock.Of<ILiveTvManager>(),
            Mock.Of<IGuideManager>(),
            Mock.Of<ITunerHostManager>(),
            Mock.Of<IListingsManager>(),
            recordingsManager,
            Mock.Of<IUserManager>(),
            Mock.Of<ILibraryManager>(),
            Mock.Of<IDtoService>(),
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<ITranscodeManager>(),
            Mock.Of<ISchedulesDirectService>());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }
}
