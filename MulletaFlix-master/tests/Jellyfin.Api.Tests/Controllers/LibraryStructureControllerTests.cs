using System;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Models.LibraryStructureDto;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection("LibraryManagerTests")]
public class LibraryStructureControllerTests
{
    private readonly Mock<IServerConfigurationManager> _mockServerConfig;
    private readonly Mock<IServerApplicationPaths> _mockAppPaths;
    private readonly Mock<ILibraryManager> _mockLibraryManager;
    private readonly Mock<ILibraryMonitor> _mockLibraryMonitor;
    private readonly Mock<ILocalizationManager> _mockLocalizationManager;
    private readonly LibraryStructureController _subject;

    public LibraryStructureControllerTests()
    {
        _mockServerConfig = new Mock<IServerConfigurationManager>();
        _mockAppPaths = new Mock<IServerApplicationPaths>();
        _mockServerConfig.Setup(c => c.ApplicationPaths).Returns(_mockAppPaths.Object);
        _mockServerConfig.Setup(c => c.Configuration).Returns(new ServerConfiguration());

        _mockLibraryManager = new Mock<ILibraryManager>();
        _mockLibraryMonitor = new Mock<ILibraryMonitor>();
        _mockLocalizationManager = new Mock<ILocalizationManager>();

        _subject = new LibraryStructureController(
            _mockServerConfig.Object,
            _mockLibraryManager.Object,
            _mockLibraryMonitor.Object,
            _mockLocalizationManager.Object,
            NullLogger<LibraryStructureController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AddVirtualFolder_NullOrWhitespaceName_ReturnsBadRequest(string? name)
    {
        var result = await _subject.AddVirtualFolder(name!, null, Array.Empty<string>(), null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Library name cannot be empty or have leading/trailing spaces.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveVirtualFolder_NullOrWhitespaceName_ReturnsBadRequest(string? name)
    {
        var result = await _subject.RemoveVirtualFolder(name!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Library name is required.", badRequest.Value);
    }

    [Fact]
    public void AddMediaPath_NullPayload_ReturnsBadRequest()
    {
        var result = _subject.AddMediaPath(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Media path data is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddMediaPath_NullOrWhitespaceName_ReturnsBadRequest(string? name)
    {
        var result = _subject.AddMediaPath(new MediaPathDto { Name = name!, Path = "C:\\media" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Library name is required.", badRequest.Value);
    }

    [Fact]
    public void AddMediaPath_NullPathAndNullPathInfo_ReturnsBadRequest()
    {
        var result = _subject.AddMediaPath(new MediaPathDto { Name = "Movies", Path = null, PathInfo = null });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Path or PathInfo is required.", badRequest.Value);
    }

    [Fact]
    public void UpdateMediaPath_NullPayload_ReturnsBadRequest()
    {
        var result = _subject.UpdateMediaPath(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Media path request is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateMediaPath_NullOrWhitespaceName_ReturnsBadRequest(string? name)
    {
        var result = _subject.UpdateMediaPath(new UpdateMediaPathRequestDto { Name = name!, PathInfo = new MediaBrowser.Model.Configuration.MediaPathInfo() });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Name must not be null or empty.", badRequest.Value);
    }

    [Fact]
    public void UpdateMediaPath_NullPathInfo_ReturnsBadRequest()
    {
        var result = _subject.UpdateMediaPath(new UpdateMediaPathRequestDto { Name = "Movies", PathInfo = null! });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("PathInfo is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RemoveMediaPath_NullOrWhitespaceName_ReturnsBadRequest(string? name)
    {
        var result = _subject.RemoveMediaPath(name!, "C:\\media");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Library name is required.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RemoveMediaPath_NullOrWhitespacePath_ReturnsBadRequest(string? path)
    {
        var result = _subject.RemoveMediaPath("Movies", path!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Media path is required.", badRequest.Value);
    }

    [Fact]
    public void UpdateLibraryOptions_NullPayload_ReturnsBadRequest()
    {
        var result = _subject.UpdateLibraryOptions(null!);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Library options request is required.", badRequest.Value);
    }

    [Fact]
    public void UpdateLibraryOptions_EmptyId_ReturnsBadRequest()
    {
        var result = _subject.UpdateLibraryOptions(new UpdateLibraryOptionsDto { Id = Guid.Empty });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Library id must not be empty.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("folder/subfolder")]
    [InlineData("folder\\subfolder")]
    public void RenameVirtualFolder_InvalidCurrentName_ReturnsBadRequest(string? name)
    {
        var result = _subject.RenameVirtualFolder(name, "ValidNewName");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Library name must be a single directory name.", badRequest.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("folder/subfolder")]
    [InlineData("folder\\subfolder")]
    public void RenameVirtualFolder_InvalidNewName_ReturnsBadRequest(string? newName)
    {
        var result = _subject.RenameVirtualFolder("ValidCurrentName", newName);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("New library name must be a single directory name.", badRequest.Value);
    }
}
