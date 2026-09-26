using System;
using System.IO;
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
}
