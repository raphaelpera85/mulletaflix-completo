using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Activity;
using MulletaFlix.Server.Implementations.Events.Consumers.Session;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Events.Consumers.Session;

/// <summary>
/// Tests for <see cref="PlaybackReportLogger"/>.
/// </summary>
public class PlaybackReportLoggerTests
{
    [Fact]
    public async Task OnEvent_PlaybackStart_EmptyPlaySessionId_StillCreatesReport()
    {
        // Web clients may report an empty (non-null) PlaySessionId and DeviceId.
        PlaybackReport? created = null;
        var manager = new Mock<IPlaybackReportManager>();
        manager.Setup(m => m.CreateAsync(It.IsAny<PlaybackReport>()))
            .Callback<PlaybackReport>(r => created = r)
            .Returns(Task.CompletedTask);

        var logger = new PlaybackReportLogger(Mock.Of<ILogger<PlaybackReportLogger>>(), manager.Object);

        var args = new PlaybackStartEventArgs
        {
            Users = new List<User> { new User("tester", "auth", "reset") },
            MediaInfo = new BaseItemDto { Name = "Movie" },
            PlaySessionId = string.Empty,
            DeviceId = string.Empty,
            DeviceName = string.Empty,
            ClientName = string.Empty
        };

        await logger.OnEvent(args);

        Assert.NotNull(created);
        Assert.False(string.IsNullOrEmpty(created!.PlaySessionId));
        Assert.False(string.IsNullOrEmpty(created.SessionId));
        Assert.Equal("Unknown", created.DeviceId);
    }
}
