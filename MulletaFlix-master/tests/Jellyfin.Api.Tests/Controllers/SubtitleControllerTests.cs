using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Subtitles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Models.SubtitleDtos;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class SubtitleControllerTests
{
    [Fact]
    public async Task UploadSubtitle_NullBody_ReturnsBadRequest()
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.UploadSubtitle(Guid.NewGuid(), null!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadSubtitle_EmptyFormat_ReturnsBadRequest(string? format)
    {
        var fixture = CreateFixture();
        var dto = new UploadSubtitleDto
        {
            Format = format!,
            Data = Convert.ToBase64String(Encoding.UTF8.GetBytes("1\n00:00:01,000 --> 00:00:02,000\nHello")),
            Language = "pt-BR"
        };

        var result = await fixture.Controller.UploadSubtitle(Guid.NewGuid(), dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadSubtitle_EmptyData_ReturnsBadRequest(string? data)
    {
        var fixture = CreateFixture();
        var dto = new UploadSubtitleDto
        {
            Format = "srt",
            Data = data!,
            Language = "pt-BR"
        };

        var result = await fixture.Controller.UploadSubtitle(Guid.NewGuid(), dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadSubtitle_InvalidBase64_ReturnsBadRequest()
    {
        var fixture = CreateFixture();
        var dto = new UploadSubtitleDto
        {
            Format = "srt",
            Data = "not-valid-base-64!!!",
            Language = "pt-BR"
        };

        var result = await fixture.Controller.UploadSubtitle(Guid.NewGuid(), dto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UploadSubtitle_ItemNotFound_ReturnsNotFound()
    {
        var fixture = CreateFixture();
        var dto = new UploadSubtitleDto
        {
            Format = "srt",
            Data = Convert.ToBase64String(Encoding.UTF8.GetBytes("1\n00:00:01,000 --> 00:00:02,000\nHello")),
            Language = "pt-BR"
        };

        fixture.LibraryManager
            .Setup(l => l.GetItemById<Video>(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Returns((Video?)null);

        var result = await fixture.Controller.UploadSubtitle(Guid.NewGuid(), dto);

        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SearchRemoteSubtitles_EmptyLanguage_ReturnsBadRequest(string? language)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.SearchRemoteSubtitles(Guid.NewGuid(), language!, null);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DownloadRemoteSubtitles_EmptySubtitleId_ReturnsBadRequest(string? subtitleId)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.DownloadRemoteSubtitles(Guid.NewGuid(), subtitleId!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetRemoteSubtitles_EmptySubtitleId_ReturnsBadRequest(string? subtitleId)
    {
        var fixture = CreateFixture();

        var result = await fixture.Controller.GetRemoteSubtitles(subtitleId!);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static (SubtitleController Controller, Mock<ILibraryManager> LibraryManager, Mock<ISubtitleManager> SubtitleManager) CreateFixture()
    {
        var serverConfig = new Mock<IServerConfigurationManager>(MockBehavior.Strict);
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Loose);
        var subtitleManager = new Mock<ISubtitleManager>(MockBehavior.Loose);
        var subtitleEncoder = new Mock<ISubtitleEncoder>(MockBehavior.Strict);
        var mediaSourceManager = new Mock<IMediaSourceManager>(MockBehavior.Strict);
        var providerManager = new Mock<IProviderManager>(MockBehavior.Strict);
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

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

        var controller = new SubtitleController(
            serverConfig.Object,
            libraryManager.Object,
            subtitleManager.Object,
            subtitleEncoder.Object,
            mediaSourceManager.Object,
            providerManager.Object,
            fileSystem.Object,
            NullLogger<SubtitleController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        return (controller, libraryManager, subtitleManager);
    }
}
