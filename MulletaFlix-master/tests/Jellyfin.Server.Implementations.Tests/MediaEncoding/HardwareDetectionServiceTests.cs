using System;
using Emby.Server.Implementations.MediaEncoding;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Server.Implementations.Tests.MediaEncoding;

public class HardwareDetectionServiceTests
{
    [Fact]
    public void Run_WhenNoHardwareEncoderSupported_FallsBackToSoftwareCpuAndSavesConfig()
    {
        var mediaEncoder = new Mock<IMediaEncoder>();
        mediaEncoder.Setup(e => e.SupportsEncoder(It.IsAny<string>())).Returns(false);

        var options = new EncodingOptions
        {
            HardwareAccelerationType = HardwareAccelerationType.nvenc,
            EnableHardwareEncoding = true,
            EncoderPreset = EncoderPreset.fast,
            H264Crf = 26,
            H265Crf = 32
        };

        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(c => c.GetConfiguration("encoding")).Returns(options);

        var appPaths = new Mock<IApplicationPaths>();

        var service = new HardwareDetectionService(
            NullLogger<HardwareDetectionService>.Instance,
            configManager.Object,
            mediaEncoder.Object,
            appPaths.Object);

        service.Run();

        Assert.Equal(HardwareAccelerationType.none, options.HardwareAccelerationType);
        Assert.False(options.EnableHardwareEncoding);
        Assert.Equal(EncoderPreset.medium, options.EncoderPreset);
        Assert.Equal(23, options.H264Crf);
        Assert.Equal(28, options.H265Crf);
        Assert.False(options.EnableTonemapping);

        configManager.Verify(c => c.SaveConfiguration("encoding", options), Times.Once);
    }

    [Fact]
    public void Run_WhenConfigAlreadyMatches_DoesNotSaveConfiguration()
    {
        var mediaEncoder = new Mock<IMediaEncoder>();
        mediaEncoder.Setup(e => e.SupportsEncoder(It.IsAny<string>())).Returns(false);

        var options = new EncodingOptions
        {
            HardwareAccelerationType = HardwareAccelerationType.nvenc,
            EnableHardwareEncoding = true
        };

        var configManager = new Mock<IServerConfigurationManager>();
        configManager.Setup(c => c.GetConfiguration("encoding")).Returns(options);

        var appPaths = new Mock<IApplicationPaths>();

        var service = new HardwareDetectionService(
            NullLogger<HardwareDetectionService>.Instance,
            configManager.Object,
            mediaEncoder.Object,
            appPaths.Object);

        // First run establishes the optimal software fallback baseline
        service.Run();
        configManager.Verify(c => c.SaveConfiguration("encoding", options), Times.Once);

        // Reset invocation counter
        configManager.Invocations.Clear();

        // Second run detects that all configurations already match and does not mutate or save
        service.Run();
        configManager.Verify(c => c.SaveConfiguration("encoding", It.IsAny<EncodingOptions>()), Times.Never);
    }
}
