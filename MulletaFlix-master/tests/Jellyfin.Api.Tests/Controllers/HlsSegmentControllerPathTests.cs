using System;
using System.IO;
using System.Security.Claims;
using MulletaFlix.Api.Constants;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class HlsSegmentControllerPathTests
{
    [Fact]
    public void GetHlsAudioSegmentLegacy_RejectsTraversalToSiblingWithSharedPrefix()
    {
        using var temp = new TemporaryDirectory();
        var transcodePath = Path.Combine(temp.Path, "transcode");
        var siblingPath = transcodePath + "-outside";
        Directory.CreateDirectory(transcodePath);
        Directory.CreateDirectory(siblingPath);
        File.WriteAllText(Path.Combine(siblingPath, "secret.mp3"), "canary");

        var controller = CreateController(transcodePath, "/Audio/item/hls/segment/stream.mp3");
        var result = controller.GetHlsAudioSegmentLegacy("item", "..\\transcode-outside\\secret");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetHlsVideoSegmentLegacy_RejectsTraversalToSiblingWithSharedPrefix()
    {
        using var temp = new TemporaryDirectory();
        var transcodePath = Path.Combine(temp.Path, "transcode");
        var siblingPath = transcodePath + "-outside";
        Directory.CreateDirectory(transcodePath);
        Directory.CreateDirectory(siblingPath);
        File.WriteAllText(Path.Combine(siblingPath, "secret.ts"), "canary");
        File.WriteAllText(Path.Combine(transcodePath, "playlist.m3u8"), "#EXTM3U");

        var controller = CreateController(transcodePath, "/Videos/item/hls/playlist/segment.ts");
        var result = controller.GetHlsVideoSegmentLegacy("item", "playlist", "..\\transcode-outside\\secret", "ts");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetHlsAudioSegmentLegacy_RejectsSymbolicLinkToOutsideFile()
    {
        using var temp = new TemporaryDirectory();
        var transcodePath = Path.Combine(temp.Path, "transcode");
        Directory.CreateDirectory(transcodePath);
        var outsideFile = Path.Combine(temp.Path, "outside.mp3");
        File.WriteAllText(outsideFile, "canary");

        try
        {
            File.CreateSymbolicLink(Path.Combine(transcodePath, "linked.mp3"), outsideFile);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            Assert.Skip($"Creating a symbolic link is unavailable in this environment: {ex.GetType().Name}.");
        }

        var controller = CreateController(transcodePath, "/Audio/item/hls/linked/stream.mp3");
        var result = controller.GetHlsAudioSegmentLegacy("item", "linked");

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void GetHlsPlaylistLegacy_AllowsPlaylistWithExpectedExtension()
    {
        using var temp = new TemporaryDirectory();
        var transcodePath = Path.Combine(temp.Path, "transcode");
        Directory.CreateDirectory(transcodePath);
        File.WriteAllText(Path.Combine(transcodePath, "playlist.m3u8"), "#EXTM3U");

        var controller = CreateController(transcodePath, "/Videos/item/hls/playlist/stream.m3u8");
        var result = controller.GetHlsPlaylistLegacy("item", "playlist");

        Assert.IsType<PhysicalFileResult>(result);
    }

    [Fact]
    public void StopEncodingProcess_RejectsDifferentDeviceFromAuthenticatedClient()
    {
        var transcodeManager = new Mock<ITranscodeManager>(MockBehavior.Strict);
        var controller = CreateController(Path.GetTempPath(), "/Videos/ActiveEncodings", transcodeManager.Object);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(InternalClaimTypes.DeviceId, "authenticated-device") },
            "test"));

        var result = controller.StopEncodingProcess("other-device", "session-1");

        Assert.IsType<ForbidResult>(result);
        transcodeManager.Verify(
            manager => manager.KillTranscodingJobs(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, bool>>()),
            Times.Never);
    }

    private static HlsSegmentController CreateController(
        string transcodePath,
        string requestPath,
        ITranscodeManager? transcodeManager = null)
    {
        var applicationPaths = new Mock<IApplicationPaths>();
        applicationPaths.Setup(paths => paths.CreateAndCheckMarker(transcodePath, "transcode", true));

        var configurationManager = new Mock<IServerConfigurationManager>();
        configurationManager
            .Setup(manager => manager.GetConfiguration("encoding"))
            .Returns(new EncodingOptions { TranscodingTempPath = transcodePath });
        configurationManager
            .SetupGet(manager => manager.CommonApplicationPaths)
            .Returns(applicationPaths.Object);

        var controller = new HlsSegmentController(
            Mock.Of<IFileSystem>(),
            configurationManager.Object,
            transcodeManager ?? Mock.Of<ITranscodeManager>());
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = requestPath;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory("mulletaflix-hls-").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
