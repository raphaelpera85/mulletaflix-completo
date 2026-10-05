using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Database.Implementations.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Trickplay;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Providers.Trickplay;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Trickplay;

public sealed class TrickplayScheduledTaskCancellationTests
{
    [Fact]
    public async Task TrickplayImagesTask_PropagatesCancellationDuringRefresh()
    {
        using var cancellation = new CancellationTokenSource();
        var libraryManager = new Mock<ILibraryManager>();
        var video = new Movie();
        libraryManager.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(1);
        libraryManager.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([video]);
        libraryManager.Setup(manager => manager.GetLibraryOptions(video)).Returns(new LibraryOptions());
        var trickplayManager = new Mock<ITrickplayManager>();
        trickplayManager.Setup(manager => manager.RefreshTrickplayDataAsync(
                video,
                false,
                It.IsAny<LibraryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((Video _, bool _, LibraryOptions _, CancellationToken token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled(token);
            });
        var task = new TrickplayImagesTask(
            NullLogger<TrickplayImagesTask>.Instance,
            libraryManager.Object,
            Mock.Of<ILocalizationManager>(),
            trickplayManager.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            task.ExecuteAsync(Mock.Of<IProgress<double>>(), cancellation.Token));
    }

    [Fact]
    public async Task TrickplayMoveImagesTask_PropagatesCancellationDuringMove()
    {
        using var cancellation = new CancellationTokenSource();
        var video = new Movie();
        var info = new TrickplayInfo { ItemId = video.Id };
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(manager => manager.GetCount(It.IsAny<InternalItemsQuery>())).Returns(1);
        libraryManager.Setup(manager => manager.GetItemList(It.IsAny<InternalItemsQuery>())).Returns([video]);
        libraryManager.Setup(manager => manager.GetLibraryOptions(video)).Returns(new LibraryOptions());
        var trickplayManager = new Mock<ITrickplayManager>();
        trickplayManager.Setup(manager => manager.GetTrickplayItemsAsync(100, 0)).ReturnsAsync([info]);
        trickplayManager.Setup(manager => manager.MoveGeneratedTrickplayDataAsync(
                video,
                It.IsAny<LibraryOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((Video _, LibraryOptions _, CancellationToken token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled(token);
            });
        var task = new TrickplayMoveImagesTask(
            NullLogger<TrickplayMoveImagesTask>.Instance,
            libraryManager.Object,
            Mock.Of<ILocalizationManager>(),
            trickplayManager.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            task.ExecuteAsync(Mock.Of<IProgress<double>>(), cancellation.Token));
    }
}
