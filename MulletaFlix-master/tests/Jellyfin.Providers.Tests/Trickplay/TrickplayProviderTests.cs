using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Trickplay;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Providers.Trickplay;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Trickplay;

public sealed class TrickplayProviderTests
{
    [Fact]
    public async Task FetchAsync_NonBlockingRefreshFailure_IsLogged()
    {
        var video = new Movie();
        var libraryOptions = new LibraryOptions { ExtractTrickplayImagesDuringLibraryScan = true };
        var trickplayOptions = new ServerConfiguration().TrickplayOptions;
        trickplayOptions.ScanBehavior = TrickplayScanBehavior.NonBlocking;
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { TrickplayOptions = trickplayOptions });

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetLibraryOptions(video)).Returns(libraryOptions);

        var failure = new InvalidOperationException("refresh failed");
        var trickplayManager = new Mock<ITrickplayManager>();
        trickplayManager.Setup(manager => manager.RefreshTrickplayDataAsync(video, false, libraryOptions, It.IsAny<CancellationToken>()))
            .Returns(Task.FromException(failure));

        var logger = new RecordingLogger();
        var provider = new TrickplayProvider(configuration.Object, trickplayManager.Object, libraryManager.Object, logger);

        var result = await provider.FetchAsync(video, new MetadataRefreshOptions(Mock.Of<IDirectoryService>()), CancellationToken.None);

        Assert.Equal(ItemUpdateType.None, result);
        Assert.Same(failure, logger.Exception);
    }

    [Fact]
    public async Task FetchAsync_NonBlockingRefreshCancellation_IsNotLoggedAsFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var video = new Movie();
        var libraryOptions = new LibraryOptions { ExtractTrickplayImagesDuringLibraryScan = true };
        var trickplayOptions = new ServerConfiguration().TrickplayOptions;
        trickplayOptions.ScanBehavior = TrickplayScanBehavior.NonBlocking;
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { TrickplayOptions = trickplayOptions });

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetLibraryOptions(video)).Returns(libraryOptions);

        var trickplayManager = new Mock<ITrickplayManager>();
        trickplayManager.Setup(manager => manager.RefreshTrickplayDataAsync(video, false, libraryOptions, cancellation.Token))
            .Returns(Task.FromCanceled(cancellation.Token));

        var logger = new RecordingLogger();
        var provider = new TrickplayProvider(configuration.Object, trickplayManager.Object, libraryManager.Object, logger);

        var result = await provider.FetchAsync(video, new MetadataRefreshOptions(Mock.Of<IDirectoryService>()), cancellation.Token);

        Assert.Equal(ItemUpdateType.None, result);
        Assert.Null(logger.Exception);
    }

    [Fact]
    public async Task FetchAsync_NonBlockingRefreshesApplyBackpressureAtCapacity()
    {
        var videos = Enumerable.Range(0, 9).Select(_ => new Movie()).ToArray();
        var libraryOptions = new LibraryOptions { ExtractTrickplayImagesDuringLibraryScan = true };
        var trickplayOptions = new ServerConfiguration().TrickplayOptions;
        trickplayOptions.ScanBehavior = TrickplayScanBehavior.NonBlocking;
        var configuration = new Mock<IServerConfigurationManager>();
        configuration.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { TrickplayOptions = trickplayOptions });

        var libraryManager = new Mock<ILibraryManager>();
        foreach (var video in videos)
        {
            libraryManager.Setup(manager => manager.GetLibraryOptions(video)).Returns(libraryOptions);
        }

        var refreshGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var trickplayManager = new Mock<ITrickplayManager>();
        trickplayManager
            .Setup(manager => manager.RefreshTrickplayDataAsync(It.IsAny<Video>(), false, libraryOptions, It.IsAny<CancellationToken>()))
            .Returns(refreshGate.Task);

        var provider = new TrickplayProvider(configuration.Object, trickplayManager.Object, libraryManager.Object, new RecordingLogger());
        var refreshOptions = new MetadataRefreshOptions(Mock.Of<IDirectoryService>());

        for (var i = 0; i < 8; i++)
        {
            await provider.FetchAsync(videos[i], refreshOptions, CancellationToken.None);
        }

        var ninthRefresh = provider.FetchAsync(videos[8], refreshOptions, CancellationToken.None);

        Assert.False(ninthRefresh.IsCompleted);
        Assert.Equal(8, trickplayManager.Invocations.Count);

        refreshGate.SetResult();
        await ninthRefresh.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(9, trickplayManager.Invocations.Count);
    }

    private sealed class RecordingLogger : ILogger<TrickplayProvider>
    {
        public Exception? Exception { get; private set; }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return new NoopScope();
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Exception = exception;
            }
        }
    }

    private sealed class NoopScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
