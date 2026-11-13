using System.Reflection;

using IntroSkipper;
using IntroSkipper.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Serialization;
using Moq;
using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class PluginRuntimeConfigurationTests : IDisposable
{
    private readonly Plugin? _previousPlugin = Plugin.Instance;

    [Fact]
    public void FFmpegPath_UsesTheCurrentServerConfigurationAfterStartup()
    {
        var encoding = new EncodingOptions { EncoderAppPathDisplay = "stale-ffmpeg.exe" };
        var paths = new Mock<IApplicationPaths>();
        paths.Setup(value => value.PluginsPath).Returns(Path.GetTempPath());
        paths.Setup(value => value.PluginConfigurationsPath).Returns(Path.GetTempPath());
        var serializer = new Mock<IXmlSerializer>();
        serializer.Setup(value => value.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>())).Returns(new PluginConfiguration());
        var serverConfiguration = new Mock<IServerConfigurationManager>();
        serverConfiguration.Setup(value => value.GetConfiguration("encoding")).Returns(() => encoding);
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(value => value.Plugins).Returns(Array.Empty<LocalPlugin>());

        var plugin = new Plugin(
            paths.Object,
            serializer.Object,
            serverConfiguration.Object,
            Mock.Of<ILibraryManager>(),
            Mock.Of<IChapterManager>(),
            pluginManager.Object);

        encoding.EncoderAppPathDisplay = "resolved-ffmpeg.exe";

        Assert.Equal("resolved-ffmpeg.exe", plugin.FFmpegPath);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(8, 2)]
    public void EffectiveMaxParallelism_StaysBetweenOneAndTwo(int configured, int expected)
    {
        Assert.Equal(expected, new PluginConfiguration { MaxParallelism = configured }.EffectiveMaxParallelism);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnalysisLengthLimit_ZeroOrNegativeValuesCannotDisableFingerprinting(int configured)
    {
        Assert.Equal(1, new PluginConfiguration { AnalysisLengthLimit = configured }.AnalysisLengthLimit);
    }

    public void Dispose()
    {
        typeof(Plugin).GetProperty(nameof(Plugin.Instance))!.GetSetMethod(nonPublic: true)!.Invoke(null, [_previousPlugin]);
    }
}
