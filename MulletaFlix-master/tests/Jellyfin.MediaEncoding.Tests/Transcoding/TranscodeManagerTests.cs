using System;
using System.IO;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.MediaEncoding.Transcoding;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.IO;
using MulletaFlix.Database.Implementations.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.MediaEncoding.Tests.Transcoding;

public class TranscodeManagerTests
{
    private readonly Mock<IServerConfigurationManager> _serverConfig = new();
    private readonly Mock<IFileSystem> _fileSystem = new();
    private readonly Mock<IMediaEncoder> _mediaEncoder = new();
    private readonly Mock<ISessionManager> _sessionManager = new();
    private readonly Mock<IUserManager> _userManager = new();
    private readonly Mock<IApplicationPaths> _appPaths = new();
    private readonly Mock<IMediaSourceManager> _mediaSourceManager = new();
    private readonly Mock<IAttachmentExtractor> _attachmentExtractor = new();

    private TranscodeManager CreateManager(int maxConcurrentJobs = 0)
    {
        var options = new EncodingOptions
        {
            MaxConcurrentTranscodingJobs = maxConcurrentJobs,
            TranscodingTempPath = Path.GetTempPath()
        };
        _serverConfig.Setup(c => c.GetConfiguration("encoding")).Returns(options);
        _serverConfig.Setup(c => c.CommonApplicationPaths).Returns(_appPaths.Object);
        _fileSystem.Setup(f => f.GetFilePaths(It.IsAny<string>(), It.IsAny<bool>())).Returns(Array.Empty<string>());

        var encodingHelper = new EncodingHelper(
            _appPaths.Object,
            _mediaEncoder.Object,
            new Mock<ISubtitleEncoder>().Object,
            new Mock<IConfiguration>().Object,
            new Mock<MediaBrowser.Common.Configuration.IConfigurationManager>().Object,
            new Mock<IPathManager>().Object);

        return new TranscodeManager(
            NullLoggerFactory.Instance,
            _fileSystem.Object,
            _appPaths.Object,
            _serverConfig.Object,
            _userManager.Object,
            _sessionManager.Object,
            encodingHelper,
            _mediaEncoder.Object,
            _mediaSourceManager.Object,
            _attachmentExtractor.Object);
    }

    [Fact]
    public void GetTranscodingJob_ReturnsNull_WhenJobNotFound()
    {
        using var manager = CreateManager();
        var job = manager.GetTranscodingJob(@"C:\transcode\test.m3u8", TranscodingJobType.Hls);
        Assert.Null(job);
    }

    [Fact]
    public void GetTranscodingJob_BySessionId_ReturnsNull_WhenNotFound()
    {
        using var manager = CreateManager();
        var job = manager.GetTranscodingJob("session-123");
        Assert.Null(job);
    }

    [Fact]
    public void OnTranscodeBeginRequest_ReturnsNull_WhenNoMatchingJob()
    {
        using var manager = CreateManager();
        var job = manager.OnTranscodeBeginRequest(@"C:\transcode\missing.m3u8", TranscodingJobType.Hls);
        Assert.Null(job);
    }

    [Fact]
    public async Task KillTranscodingJobs_ExecutesWithoutError_WhenNoActiveJobs()
    {
        using var manager = CreateManager(maxConcurrentJobs: 4);
        await manager.KillTranscodingJobs("device-1", "session-1", _ => true);
    }

    [Fact]
    public async Task KillTranscodingJobs_DoesNotKillOtherDevice_WhenPlaySessionIdMatches()
    {
        using var manager = CreateManager(maxConcurrentJobs: 4);
        AddJob(manager, "device-1", "session-shared", @"C:\transcode\device-1.m3u8");
        AddJob(manager, "device-2", "session-shared", @"C:\transcode\device-2.m3u8");

        await manager.KillTranscodingJobs("device-1", "session-shared", _ => false);

        Assert.Equal(1, manager.ActiveTranscodingJobsCount);
        Assert.Null(manager.GetTranscodingJob("device-1", "session-shared"));
        Assert.NotNull(manager.GetTranscodingJob("device-2", "session-shared"));
    }

    [Fact]
    public async Task KillTranscodingJob_PassesOwnerToLiveStreamClose()
    {
        using var manager = CreateManager(maxConcurrentJobs: 4);
        var userId = Guid.NewGuid();
        var job = AddJob(manager, "device-1", "play-session", @"C:\transcode\owned.m3u8");
        job.LiveStreamId = "owned-live-stream";
        job.UserId = userId;

        var killMethod = typeof(TranscodeManager).GetMethod("KillTranscodingJob", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)killMethod.Invoke(manager, [job, true, (Func<string, bool>)(_ => false)])!;
        await task;

        _sessionManager.Verify(
            sessionManager => sessionManager.CloseLiveStreamIfNeededAsync("owned-live-stream", "play-session", userId),
            Times.Once);
    }

    [Fact]
    public void CreateLiveStreamRequest_PreservesStreamStateUser()
    {
        var user = new User("test", "test", "test") { Id = Guid.NewGuid() };
        using var state = new MediaBrowser.Controller.Streaming.StreamState(
            _mediaSourceManager.Object,
            TranscodingJobType.Hls,
            Mock.Of<ITranscodeManager>())
        {
            User = user,
            MediaSource = new MediaBrowser.Model.Dto.MediaSourceInfo { OpenToken = "open-token" }
        };

        var createRequest = typeof(TranscodeManager).GetMethod("CreateLiveStreamRequest", BindingFlags.Static | BindingFlags.NonPublic)!;
        var request = (MediaBrowser.Model.MediaInfo.LiveStreamRequest)createRequest.Invoke(null, [state])!;

        Assert.Equal(user.Id, request.UserId);
        Assert.Equal("open-token", request.OpenToken);
    }

    [Fact]
    public void PingTranscodingJob_OnlyUpdatesMatchingDevice_WhenPlaySessionIdMatches()
    {
        using var manager = CreateManager(maxConcurrentJobs: 4);
        var firstDeviceJob = AddJob(manager, "device-1", "session-shared", @"C:\transcode\device-1.m3u8");
        var secondDeviceJob = AddJob(manager, "device-2", "session-shared", @"C:\transcode\device-2.m3u8");

        manager.PingTranscodingJob("device-1", "session-shared", true);

        Assert.True(firstDeviceJob.IsUserPaused);
        Assert.False(secondDeviceJob.IsUserPaused);
    }

    private static TranscodingJob AddJob(TranscodeManager manager, string deviceId, string playSessionId, string path)
    {
        var jobsField = typeof(TranscodeManager).GetField("_activeTranscodingJobs", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var jobs = (ConcurrentDictionary<string, TranscodingJob>)jobsField.GetValue(manager)!;
        var job = new TranscodingJob(NullLogger<TranscodingJob>.Instance)
        {
            DeviceId = deviceId,
            PlaySessionId = playSessionId,
            Path = path,
            Type = TranscodingJobType.Progressive,
            HasExited = true
        };
        Assert.True(jobs.TryAdd(path, job));
        return job;
    }

    [Fact]
    public void ActiveTranscodingJobsCount_InitiallyZero()
    {
        using var manager = CreateManager(maxConcurrentJobs: 4);
        Assert.Equal(0, manager.ActiveTranscodingJobsCount);
    }
}
