using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emby.Naming.Common;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Providers.Subtitles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.Subtitles;

public sealed class SubtitleManagerPathSafetyTests
{
    [Theory]
    [InlineData("pob")]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("zh-Hant")]
    [InlineData("pt_BR")]
    [InlineData("x-1")]
    public void IsSafeSubtitleLanguage_AcceptsLanguageTokens(string language)
    {
        Assert.True(SubtitleManager.IsSafeSubtitleLanguage(language));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pt/BR")]
    [InlineData("pt\\BR")]
    [InlineData("pt:stream")]
    [InlineData("pt\n")]
    [InlineData("日本語")]
    public void IsSafeSubtitleLanguage_RejectsPathAndNonTokenCharacters(string language)
    {
        Assert.False(SubtitleManager.IsSafeSubtitleLanguage(language));
    }

    [Fact]
    public async Task DownloadSubtitles_RejectsUnsafeLanguageBeforeCreatingFile()
    {
        using var temp = new TemporaryDirectory();
        var providerName = "Test";
        var subtitleStream = new MemoryStream(Encoding.UTF8.GetBytes("subtitle"));
        var provider = new Mock<ISubtitleProvider>();
        provider.SetupGet(p => p.Name).Returns(providerName);
        provider.Setup(p => p.GetSubtitles("remote-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubtitleResponse
            {
                Language = "pt:stream",
                Format = "srt",
                Stream = subtitleStream
            });

        var manager = new SubtitleManager(
            NullLogger<SubtitleManager>.Instance,
            Mock.Of<IFileSystem>(),
            Mock.Of<ILibraryMonitor>(),
            Mock.Of<IMediaSourceManager>(),
            Mock.Of<ILocalizationManager>(),
            [provider.Object],
            new NamingOptions());
        var item = new Movie { Path = Path.Combine(temp.Path, "movie.mkv") };
        var providerId = providerName.ToLowerInvariant().GetMD5().ToString("N", CultureInfo.InvariantCulture);

        await Assert.ThrowsAsync<ArgumentException>(() => manager.DownloadSubtitles(
            item,
            new LibraryOptions { SaveSubtitlesWithMedia = true },
            providerId + "_remote-id",
            CancellationToken.None));

        Assert.False(subtitleStream.CanRead);
        Assert.Empty(Directory.GetFiles(temp.Path, "*", SearchOption.AllDirectories));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory("mulletaflix-subtitles-").FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
