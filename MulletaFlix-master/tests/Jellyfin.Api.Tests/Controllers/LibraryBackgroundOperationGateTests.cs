using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Models.LibraryStructureDto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class LibraryBackgroundOperationGateTests
{
    [Fact]
    public void TryAcquire_RejectsConcurrentOperationAndReleasesIdempotently()
    {
        var firstLease = LibraryBackgroundOperationGate.TryAcquire();
        Assert.NotNull(firstLease);

        try
        {
            Assert.Null(LibraryBackgroundOperationGate.TryAcquire());
        }
        finally
        {
            firstLease.Dispose();
        }

        firstLease.Dispose();
        using var nextLease = LibraryBackgroundOperationGate.TryAcquire();
        Assert.NotNull(nextLease);
    }

    [Fact]
    public void AddMediaPath_WhenBackgroundOperationIsActive_Returns429WithoutMutatingLibrary()
    {
        using var activeLease = LibraryBackgroundOperationGate.TryAcquire();
        Assert.NotNull(activeLease);

        var paths = Mock.Of<IServerApplicationPaths>();
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.ApplicationPaths).Returns(paths);
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        var controller = new LibraryStructureController(
            configuration.Object,
            libraryManager.Object,
            Mock.Of<ILibraryMonitor>(),
            Mock.Of<ILocalizationManager>(),
            NullLogger<LibraryStructureController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = controller.AddMediaPath(null!, refreshLibrary: true);

        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.Equal("1", controller.Response.Headers.RetryAfter.ToString());
        libraryManager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AddMediaPath_WhenRefreshFails_RestartsMonitorAndReleasesAdmission()
    {
        var monitorRestarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var monitor = new Mock<ILibraryMonitor>(MockBehavior.Strict);
        monitor.Setup(libraryMonitor => libraryMonitor.Stop());
        monitor.Setup(libraryMonitor => libraryMonitor.Start()).Callback(() => monitorRestarted.TrySetResult());

        var paths = Mock.Of<IServerApplicationPaths>();
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.ApplicationPaths).Returns(paths);
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager.Setup(manager => manager.AddMediaPath("Movies", It.IsAny<MediaPathInfo>()));
        libraryManager
            .Setup(manager => manager.ValidateMediaLibrary(It.IsAny<IProgress<double>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(new InvalidOperationException("simulated scan failure")));
        var controller = new LibraryStructureController(
            configuration.Object,
            libraryManager.Object,
            monitor.Object,
            Mock.Of<ILocalizationManager>(),
            NullLogger<LibraryStructureController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = controller.AddMediaPath(new MediaPathDto { Name = "Movies", Path = "D:\\Movies" }, refreshLibrary: true);

        Assert.IsType<NoContentResult>(result);
        await monitorRestarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        monitor.Verify(libraryMonitor => libraryMonitor.Start(), Times.Once);
        libraryManager.VerifyAll();
        using var nextLease = LibraryBackgroundOperationGate.TryAcquire();
        Assert.NotNull(nextLease);
    }
}
