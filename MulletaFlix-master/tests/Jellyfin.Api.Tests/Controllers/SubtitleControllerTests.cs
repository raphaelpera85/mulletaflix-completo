using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class SubtitleControllerTests
{
    [Fact]
    public void SubtitleEndpoints_RequireAuthorization()
    {
        AssertMethodHasAuthorize(nameof(SubtitleController.GetSubtitle));
        AssertMethodHasAuthorize(nameof(SubtitleController.GetSubtitleWithTicks));
    }

    [Fact]
    public async Task GetSubtitle_WhenItemIsNotAccessible_ReturnsNotFound()
    {
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(m => m.GetItemById<Video>(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns((Video?)null);

        var controller = CreateController(libraryManager.Object);

        var result = await controller.GetSubtitle(
            Guid.NewGuid(),
            "source",
            0,
            "srt",
            null,
            null,
            null,
            null,
            null,
            false,
            false,
            0);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetFallbackFontList_WhenPathEmpty_ReturnsEmptyArray()
    {
        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(c => c.GetConfiguration("encoding"))
            .Returns(new MediaBrowser.Model.Configuration.EncodingOptions { FallbackFontPath = string.Empty });

        var controller = CreateController(configManager: configManager.Object);

        var result = controller.GetFallbackFontList();

        var okResult = Assert.IsAssignableFrom<OkObjectResult>(result.Result);
        var fonts = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<MediaBrowser.Model.Subtitles.FontFile>>(okResult.Value);
        Assert.Empty(fonts);
    }

    [Fact]
    public void GetFallbackFontList_WhenCalled_ReturnsOkWithETagAndCacheHeaders()
    {
        var fontDir = @"C:\fonts";
        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(c => c.GetConfiguration("encoding"))
            .Returns(new MediaBrowser.Model.Configuration.EncodingOptions { FallbackFontPath = fontDir });

        var fileSystem = new Mock<IFileSystem>();
        var sampleFile = new FileSystemMetadata
        {
            FullName = @"C:\fonts\test.ttf",
            Name = "test.ttf",
            Length = 1024
        };
        fileSystem.Setup(f => f.GetFiles(fontDir, It.IsAny<System.Collections.Generic.IReadOnlyList<string>>(), false, false))
            .Returns(new[] { sampleFile });
        fileSystem.Setup(f => f.GetCreationTimeUtc(sampleFile)).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        fileSystem.Setup(f => f.GetLastWriteTimeUtc(sampleFile)).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var controller = CreateController(configManager: configManager.Object, fileSystem: fileSystem.Object);

        var result = controller.GetFallbackFontList();

        var okResult = Assert.IsAssignableFrom<OkObjectResult>(result.Result);
        var fonts = Assert.IsAssignableFrom<System.Collections.Generic.IEnumerable<MediaBrowser.Model.Subtitles.FontFile>>(okResult.Value);
        Assert.Single(fonts);

        var responseHeaders = controller.Response.Headers;
        Assert.True(responseHeaders.ContainsKey("ETag"));
        Assert.Equal("public, max-age=300", responseHeaders.CacheControl.ToString());
    }

    [Fact]
    public void GetFallbackFontList_WhenIfNoneMatchMatches_Returns304NotModified()
    {
        var fontDir = @"C:\fonts";
        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(c => c.GetConfiguration("encoding"))
            .Returns(new MediaBrowser.Model.Configuration.EncodingOptions { FallbackFontPath = fontDir });

        var fileSystem = new Mock<IFileSystem>();
        var sampleFile = new FileSystemMetadata
        {
            FullName = @"C:\fonts\test.ttf",
            Name = "test.ttf",
            Length = 1024
        };
        fileSystem.Setup(f => f.GetFiles(fontDir, It.IsAny<System.Collections.Generic.IReadOnlyList<string>>(), false, false))
            .Returns(new[] { sampleFile });
        fileSystem.Setup(f => f.GetCreationTimeUtc(sampleFile)).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        fileSystem.Setup(f => f.GetLastWriteTimeUtc(sampleFile)).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Call once to discover the ETag
        var controller1 = CreateController(configManager: configManager.Object, fileSystem: fileSystem.Object);
        controller1.GetFallbackFontList();
        var etag = controller1.Response.Headers.ETag.ToString();
        Assert.False(string.IsNullOrEmpty(etag));

        // Call second time with If-None-Match header
        var controller2 = CreateController(configManager: configManager.Object, fileSystem: fileSystem.Object);
        controller2.Request.Headers.IfNoneMatch = etag;

        var result2 = controller2.GetFallbackFontList();

        var statusCodeResult = Assert.IsType<StatusCodeResult>(result2.Result);
        Assert.Equal(StatusCodes.Status304NotModified, statusCodeResult.StatusCode);
        Assert.Equal("public, max-age=300", controller2.Response.Headers.CacheControl.ToString());
    }

    private static SubtitleController CreateController(
        ILibraryManager? libraryManager = null,
        IServerConfigurationManager? configManager = null,
        IFileSystem? fileSystem = null)
    {
        return new SubtitleController(
            configManager ?? Mock.Of<IServerConfigurationManager>(),
            libraryManager ?? Mock.Of<ILibraryManager>(),
            Mock.Of<ISubtitleManager>(),
            Mock.Of<ISubtitleEncoder>(),
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<IProviderManager>(),
            fileSystem ?? Mock.Of<IFileSystem>(),
            Mock.Of<ILogger<SubtitleController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[]
                            {
                                new Claim(InternalClaimTypes.UserId, Guid.NewGuid().ToString("N"))
                            },
                            "test"))
                }
            }
        };
    }

    private static void AssertMethodHasAuthorize(string methodName)
    {
        var method = typeof(SubtitleController).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        Assert.Contains(method!.GetCustomAttributes<AuthorizeAttribute>(inherit: true), _ => true);
    }
}
