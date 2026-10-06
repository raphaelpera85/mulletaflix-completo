using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Lyrics;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Lyrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class LyricsControllerTests
{
    private readonly Mock<ILibraryManager> _libraryManager = new();
    private readonly Mock<ILyricManager> _lyricManager = new();
    private readonly Mock<IProviderManager> _providerManager = new();
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IUserManager> _userManager = new();
    private readonly Guid _itemId = Guid.NewGuid();
    private readonly Audio _audio = new();

    public LyricsControllerTests()
    {
        _libraryManager
            .Setup(m => m.GetItemById<Audio>(_itemId, It.IsAny<Guid>()))
            .Returns(_audio);
    }

    [Fact]
    public async Task UploadLyrics_AcceptsWellFormedLrcFile()
    {
        var controller = CreateController("[00:01.00]Hello world\n[00:02.00]Second line\n");

        var result = await controller.UploadLyrics(_itemId, "lyrics.lrc");

        Assert.IsType<MulletaFlix.Api.Results.OkResult<LyricDto>>(result.Result);
        _lyricManager.Verify(
            m => m.SaveLyricAsync(_audio, "lrc", It.IsAny<Stream>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadLyrics_RejectsUnsupportedExtension()
    {
        var controller = CreateController("1\n00:00:01,000 --> 00:00:02,000\nHello\n");

        var result = await controller.UploadLyrics(_itemId, "lyrics.srt");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("Unsupported lyric format", (string)badRequest.Value!, StringComparison.Ordinal);
        _lyricManager.Verify(m => m.SaveLyricAsync(It.IsAny<Audio>(), It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);
    }

    [Fact]
    public async Task UploadLyrics_RejectsEmbeddedScriptPayload()
    {
        var controller = CreateController("[00:01.00]<script>document.location='//evil.example'</script>");

        var result = await controller.UploadLyrics(_itemId, "lyrics.lrc");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("disallowed content", (string)badRequest.Value!, StringComparison.Ordinal);
        _lyricManager.Verify(m => m.SaveLyricAsync(It.IsAny<Audio>(), It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);
    }

    [Fact]
    public async Task UploadLyrics_RejectsBinaryPayloadMasqueradingAsLrc()
    {
        byte[] payload = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];
        var controller = CreateController(payload);

        var result = await controller.UploadLyrics(_itemId, "lyrics.lrc");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Contains("does not appear to be valid text", (string)badRequest.Value!, StringComparison.Ordinal);
        _lyricManager.Verify(m => m.SaveLyricAsync(It.IsAny<Audio>(), It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);
    }

    [Fact]
    public async Task UploadLyrics_RejectsBodyLargerThanOneMegabyte()
    {
        // Content-Length is intentionally left unset to exercise the post-read body-size check
        // (the 1 MB cap must hold even when a chunked request omits Content-Length).
        var oversized = new string('a', 1_048_577);
        var controller = CreateController(oversized, setContentLength: false);

        var result = await controller.UploadLyrics(_itemId, "lyrics.txt");

        var tooLarge = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status413RequestEntityTooLarge, tooLarge.StatusCode);
        _lyricManager.Verify(m => m.SaveLyricAsync(It.IsAny<Audio>(), It.IsAny<string>(), It.IsAny<Stream>()), Times.Never);
    }

    private LyricsController CreateController(string body, bool setContentLength = true)
        => CreateController(Encoding.UTF8.GetBytes(body), setContentLength);

    private LyricsController CreateController(byte[] body, bool setContentLength = true)
    {
        _lyricManager
            .Setup(m => m.SaveLyricAsync(It.IsAny<Audio>(), It.IsAny<string>(), It.IsAny<Stream>()))
            .ReturnsAsync(new LyricDto());

        var controller = new LyricsController(
            _libraryManager.Object,
            _lyricManager.Object,
            _providerManager.Object,
            _fileSystem.Object,
            _userManager.Object);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(InternalClaimTypes.UserId, Guid.NewGuid().ToString("N"))],
                "test"))
        };
        httpContext.Request.Body = new MemoryStream(body);
        if (setContentLength)
        {
            httpContext.Request.ContentLength = body.Length;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
