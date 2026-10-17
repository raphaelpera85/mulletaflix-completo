using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Controllers;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class LibraryRefreshTests
{
    [Fact]
    public async Task RefreshLibrary_WhenScanSucceeds_ReturnsNoContent()
    {
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.ValidateMediaLibrary(It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var controller = CreateController(libraryManager.Object);

        var result = await controller.RefreshLibrary();

        Assert.IsType<NoContentResult>(result);
        libraryManager.VerifyAll();
    }

    [Fact]
    public async Task RefreshLibrary_WhenScanFails_ReturnsInternalServerError()
    {
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(manager => manager.ValidateMediaLibrary(It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new InvalidOperationException("simulated scan failure")));
        var controller = CreateController(libraryManager.Object);

        var result = await controller.RefreshLibrary();

        var statusResult = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
        libraryManager.VerifyAll();
    }

    private static LibraryController CreateController(ILibraryManager libraryManager)
    {
        return new LibraryController(
            Mock.Of<IProviderManager>(),
            Mock.Of<ISimilarItemsManager>(),
            libraryManager,
            Mock.Of<IUserManager>(),
            Mock.Of<ICollectionManager>(),
            Mock.Of<IDtoService>(),
            Mock.Of<IActivityManager>(),
            Mock.Of<ILocalizationManager>(),
            Mock.Of<ILibraryMonitor>(),
            NullLogger<LibraryController>.Instance,
            Mock.Of<IServerConfigurationManager>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
