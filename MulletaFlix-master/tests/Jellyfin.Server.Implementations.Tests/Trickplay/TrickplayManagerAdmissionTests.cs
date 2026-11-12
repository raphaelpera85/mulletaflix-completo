using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Trickplay;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Implementations.Trickplay;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Trickplay;

public sealed class TrickplayManagerAdmissionTests
{
    [Fact]
    public async Task RefreshTrickplayDataAsync_PropagatesRequestedCancellationFromImageExtraction()
    {
        var cancellation = new CancellationTokenSource();
        var mediaPath = Path.GetTempFileName();
        var trickplayPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(trickplayPath);

        try
        {
            var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot())
                .Options;
            var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
            databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
            var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
            var context = new MulletaFlixDbContext(options, NullLogger<MulletaFlixDbContext>.Instance, databaseProvider.Object, lockingBehavior.Object);
            var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
            dbFactory.Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(context);

            var mediaStream = new MediaStream { Type = MediaStreamType.Video, Width = 640 };
            var video = new TestMovie
            {
                RunTimeTicks = TimeSpan.FromMinutes(2).Ticks,
                VideoType = VideoType.VideoFile,
                Path = mediaPath,
                MediaSource = new MediaSourceInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Path = mediaPath,
                    Container = "mkv",
                    MediaStreams = [mediaStream]
                }
            };
            video.MediaSource.Id = video.Id.ToString();
            Assert.True(video.IsCompleteMedia);
            Assert.NotEmpty(video.GetMediaStreams());
            Assert.Equal(video.Id, Guid.Parse(Assert.Single(video.GetMediaSources(false)).Id));
            Assert.True(File.Exists(mediaPath));

            var mediaEncoder = new Mock<IMediaEncoder>();
            mediaEncoder.Setup(encoder => encoder.ExtractVideoImagesOnIntervalAccelerated(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MediaSourceInfo>(),
                    It.IsAny<MediaStream>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<System.Diagnostics.ProcessPriorityClass?>(),
                    It.IsAny<bool>(),
                    It.IsAny<EncodingHelper>(),
                    It.IsAny<CancellationToken>()))
                .Returns((string _, string _, MediaSourceInfo _, MediaStream _, int _, TimeSpan _, bool _, bool _, int? _, int? _, System.Diagnostics.ProcessPriorityClass? _, bool _, EncodingHelper _, CancellationToken _) =>
                {
                    cancellation.Cancel();
                    return Task.FromCanceled<string>(cancellation.Token);
                });

            var serverConfiguration = new ServerConfiguration();
            var configurationManager = new Mock<IServerConfigurationManager>();
            configurationManager.SetupGet(manager => manager.Configuration).Returns(serverConfiguration);
            var pathManager = new Mock<IPathManager>();
            pathManager.Setup(manager => manager.GetTrickplayDirectory(It.IsAny<BaseItem>(), It.IsAny<bool>())).Returns(trickplayPath);
            var appPaths = Mock.Of<IApplicationPaths>();
            var encodingHelper = new EncodingHelper(
                appPaths,
                mediaEncoder.Object,
                Mock.Of<ISubtitleEncoder>(),
                Mock.Of<IConfiguration>(),
                Mock.Of<MediaBrowser.Common.Configuration.IConfigurationManager>(),
                pathManager.Object);
            var manager = new TrickplayManager(
                NullLogger<TrickplayManager>.Instance,
                mediaEncoder.Object,
                Mock.Of<IFileSystem>(),
                encodingHelper,
                configurationManager.Object,
                Mock.Of<IImageEncoder>(),
                dbFactory.Object,
                appPaths,
                pathManager.Object,
                Mock.Of<IMemoryCache>());

            var refresh = manager.RefreshTrickplayDataAsync(
                video,
                false,
                new LibraryOptions { EnableTrickplayImageExtraction = true },
                cancellation.Token);
            mediaEncoder.Verify(
                encoder => encoder.ExtractVideoImagesOnIntervalAccelerated(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MediaSourceInfo>(),
                    It.IsAny<MediaStream>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<System.Diagnostics.ProcessPriorityClass?>(),
                    It.IsAny<bool>(),
                    It.IsAny<EncodingHelper>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => refresh);
        }
        finally
        {
            cancellation.Dispose();
            File.Delete(mediaPath);
            Directory.Delete(trickplayPath, true);
        }
    }

    [Fact]
    public async Task RefreshTrickplayDataAsync_InvalidEncoderDirectoryDoesNotEscapeFromCleanup()
    {
        var mediaPath = Path.GetTempFileName();
        var invalidTempPath = Path.GetTempFileName();

        try
        {
            var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"), new InMemoryDatabaseRoot())
                .Options;
            var databaseProvider = new Mock<IMulletaFlixDatabaseProvider>();
            databaseProvider.Setup(provider => provider.OnModelCreating(It.IsAny<ModelBuilder>()));
            var lockingBehavior = new Mock<IEntityFrameworkCoreLockingBehavior>();
            var context = new MulletaFlixDbContext(options, NullLogger<MulletaFlixDbContext>.Instance, databaseProvider.Object, lockingBehavior.Object);
            var dbFactory = new Mock<IDbContextFactory<MulletaFlixDbContext>>();
            dbFactory.Setup(factory => factory.CreateDbContextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(context);

            var video = new TestMovie
            {
                RunTimeTicks = TimeSpan.FromMinutes(2).Ticks,
                VideoType = VideoType.VideoFile,
                Path = mediaPath,
                MediaSource = new MediaSourceInfo
                {
                    Id = Guid.Empty.ToString(),
                    Path = mediaPath,
                    Container = "mkv",
                    MediaStreams = [new MediaStream { Type = MediaStreamType.Video, Width = 640 }]
                }
            };
            video.MediaSource.Id = video.Id.ToString();
            Assert.True(video.IsCompleteMedia);

            var mediaEncoder = new Mock<IMediaEncoder>();
            mediaEncoder.Setup(encoder => encoder.ExtractVideoImagesOnIntervalAccelerated(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<MediaSourceInfo>(),
                    It.IsAny<MediaStream>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<bool>(),
                    It.IsAny<bool>(),
                    It.IsAny<int?>(),
                    It.IsAny<int?>(),
                    It.IsAny<System.Diagnostics.ProcessPriorityClass?>(),
                    It.IsAny<bool>(),
                    It.IsAny<EncodingHelper>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(invalidTempPath);

            var manager = CreateManager(dbFactory.Object, mediaEncoder.Object);

            await manager.RefreshTrickplayDataAsync(
                video,
                false,
                new LibraryOptions { EnableTrickplayImageExtraction = true },
                CancellationToken.None);

            mediaEncoder.Verify(encoder => encoder.ExtractVideoImagesOnIntervalAccelerated(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<MediaSourceInfo>(),
                It.IsAny<MediaStream>(),
                It.IsAny<int>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<int?>(),
                It.IsAny<System.Diagnostics.ProcessPriorityClass?>(),
                It.IsAny<bool>(),
                It.IsAny<EncodingHelper>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.True(File.Exists(invalidTempPath));
        }
        finally
        {
            File.Delete(mediaPath);
            File.Delete(invalidTempPath);
        }
    }

    [Fact]
    public async Task RefreshTrickplayDataAsync_LimitsDatabaseContextCreationToOneActiveRefresh()
    {
        var factory = new BlockingDbContextFactory();
        var manager = CreateManager(factory);
        var libraryOptions = new LibraryOptions();
        var video = new TestMovie
        {
            RunTimeTicks = TimeSpan.FromMinutes(2).Ticks,
            VideoType = VideoType.VideoFile
        };
        using var firstCancellation = new CancellationTokenSource();
        using var secondCancellation = new CancellationTokenSource();
        var pendingMeasurements = new List<long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == TrickplayManager.MeterName
                && instrument.Name == "mulletaflix.trickplay.refresh.pending")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "mulletaflix.trickplay.refresh.pending")
            {
                pendingMeasurements.Add(measurement);
            }
        });
        listener.Start();
        listener.RecordObservableInstruments();
        var initialPending = Assert.Single(pendingMeasurements);
        pendingMeasurements.Clear();
        Task? firstRefresh = null;
        Task? secondRefresh = null;

        try
        {
            firstRefresh = manager.RefreshTrickplayDataAsync(video, false, libraryOptions, firstCancellation.Token);
            await factory.WaitForCallAsync(1).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            secondRefresh = manager.RefreshTrickplayDataAsync(video, false, libraryOptions, secondCancellation.Token);

            Assert.Equal(1, factory.CallCount);
            listener.RecordObservableInstruments();
            Assert.True(Assert.Single(pendingMeasurements) > initialPending);
            pendingMeasurements.Clear();

            secondCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondRefresh);
            listener.RecordObservableInstruments();
            Assert.Equal(initialPending, Assert.Single(pendingMeasurements));
            Assert.Equal(1, factory.CallCount);

            firstCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstRefresh);
        }
        finally
        {
            firstCancellation.Cancel();
            secondCancellation.Cancel();
            await IgnoreCancellationAsync(firstRefresh);
            await IgnoreCancellationAsync(secondRefresh);
        }
    }

    private static TrickplayManager CreateManager(IDbContextFactory<MulletaFlixDbContext> dbFactory, IMediaEncoder? encoder = null)
    {
        var appPaths = Mock.Of<IApplicationPaths>();
        var mediaEncoder = encoder ?? Mock.Of<IMediaEncoder>();
        var pathManagerMock = new Mock<IPathManager>();
        pathManagerMock
            .Setup(manager => manager.GetTrickplayDirectory(It.IsAny<BaseItem>(), It.IsAny<bool>()))
            .Returns(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var pathManager = pathManagerMock.Object;
        var encodingHelper = new EncodingHelper(
            appPaths,
            mediaEncoder,
            Mock.Of<ISubtitleEncoder>(),
            Mock.Of<IConfiguration>(),
            Mock.Of<MediaBrowser.Common.Configuration.IConfigurationManager>(),
            pathManager);
        var serverConfiguration = new ServerConfiguration();
        var serverConfigurationManager = new Mock<IServerConfigurationManager>();
        serverConfigurationManager.SetupGet(manager => manager.Configuration).Returns(serverConfiguration);

        return new TrickplayManager(
            NullLogger<TrickplayManager>.Instance,
            mediaEncoder,
            Mock.Of<IFileSystem>(),
            encodingHelper,
            serverConfigurationManager.Object,
            Mock.Of<IImageEncoder>(),
            dbFactory,
            appPaths,
            pathManager,
            Mock.Of<IMemoryCache>());
    }

    private static async Task IgnoreCancellationAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed class TestMovie : Movie
    {
        public MediaSourceInfo MediaSource { get; set; } = new();

        public override IReadOnlyList<MediaStream> GetMediaStreams() => [new MediaStream()];

        public override IReadOnlyList<MediaSourceInfo> GetMediaSources(bool enablePathSubstitution) => [MediaSource];

        protected override bool IsActiveRecording() => false;
    }

    private sealed class BlockingDbContextFactory : IDbContextFactory<MulletaFlixDbContext>
    {
        private readonly TaskCompletionSource[] _entered =
        [
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously)
        ];
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public MulletaFlixDbContext CreateDbContext() => throw new NotSupportedException();

        public async Task<MulletaFlixDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _callCount);
            _entered[call - 1].TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Canceled DB-context creation should not complete successfully.");
        }

        public Task WaitForCallAsync(int call) => _entered[call - 1].Task;
    }
}
