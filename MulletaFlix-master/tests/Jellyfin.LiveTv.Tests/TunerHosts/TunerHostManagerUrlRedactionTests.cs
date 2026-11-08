using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.LiveTv.Configuration;
using MulletaFlix.LiveTv.TunerHosts;
using Xunit;

namespace Jellyfin.LiveTv.Tests.TunerHosts;

public sealed class TunerHostManagerUrlRedactionTests
{
    [Fact]
    public async Task ScanForTunerDeviceChanges_RedactsDiscoveredAndChangedUrlsFromLogs()
    {
        var previousUrl = "https://provider.example/old";
        var credentialUrl = "https://alice:secret@provider.example/alice/secret/123.ts?api_key=token";
        var options = new LiveTvOptions
        {
            TunerHosts =
            [
                new TunerHostInfo
                {
                    Id = "tuner-id",
                    Type = "m3u",
                    DeviceId = "device-id",
                    Url = previousUrl
                }
            ]
        };
        var configuration = new Mock<IConfigurationManager>();
        configuration.Setup(x => x.GetConfiguration("livetv")).Returns(options);
        var host = new Mock<ITunerHost>();
        host.SetupGet(x => x.IsSupported).Returns(true);
        host.SetupGet(x => x.Name).Returns("Test tuner");
        host.SetupGet(x => x.Type).Returns("m3u");
        host.Setup(x => x.DiscoverDevices(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TunerHostInfo
                {
                    Id = "tuner-id",
                    Type = "m3u",
                    DeviceId = "device-id",
                    Url = credentialUrl
                }
            ]);
        var logger = new CapturingLogger<TunerHostManager>();
        var manager = new TunerHostManager(logger, configuration.Object, Mock.Of<ITaskManager>(), [host.Object]);

        await manager.ScanForTunerDeviceChanges(CancellationToken.None);

        Assert.Equal(2, logger.Messages.Count);
        Assert.All(logger.Messages, message =>
        {
            Assert.DoesNotContain("alice", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("api_key", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("provider.example", message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task DiscoverTuners_RedactsUrlFromDiscoveryException()
    {
        var credentialUrl = "https://alice:secret@provider.example/alice/secret/123.ts?api_key=token";
        var configuration = new Mock<IConfigurationManager>();
        configuration.Setup(x => x.GetConfiguration("livetv")).Returns(new LiveTvOptions());
        var host = new Mock<ITunerHost>();
        host.SetupGet(x => x.IsSupported).Returns(true);
        host.Setup(x => x.DiscoverDevices(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException($"Request failed for {credentialUrl}"));
        var logger = new CapturingLogger<TunerHostManager>();
        var manager = new TunerHostManager(logger, configuration.Object, Mock.Of<ITaskManager>(), [host.Object]);

        await foreach (var tuner in manager.DiscoverTuners(newDevicesOnly: false))
        {
            Assert.NotNull(tuner);
        }

        var message = Assert.Single(logger.Messages);
        Assert.DoesNotContain("alice", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key=token", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("provider.example", message, StringComparison.Ordinal);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            where TState : notnull
            => Messages.Add(formatter(state, exception));
    }
}
