using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MulletaFlix.Extensions;
using MulletaFlix.LiveTv.Recordings;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.LiveTv;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.LiveTv.Tests.Recordings;

public sealed class RecordingsMetadataManagerTests
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "MulletaFlix-test-" + Guid.NewGuid());

    [Fact]
    public async Task SaveRecordingMetadata_DateAddedIsUtc()
    {
        Directory.CreateDirectory(_tempDir);
        var recordingPath = Path.Combine(_tempDir, "test-recording.ts");
        FileHelper.CreateEmpty(recordingPath);

        var config = new Mock<IConfigurationManager>();
        config.Setup(c => c.GetConfiguration("livetv"))
            .Returns(new LiveTvOptions { SaveRecordingNFO = true, SaveRecordingImages = false });
        config.Setup(c => c.GetConfiguration("xbmcmetadata"))
            .Returns(new XbmcMetadataOptions());

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(l => l.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns(Array.Empty<BaseItem>());

        var manager = new RecordingsMetadataManager(
            NullLogger<RecordingsMetadataManager>.Instance,
            config.Object,
            libraryManager.Object);

        var timer = new TimerInfo { Name = "Test Recording", ProgramId = null };

        var beforeUtc = DateTime.UtcNow.AddSeconds(-2);
        await manager.SaveRecordingMetadata(timer, recordingPath, null, TestContext.Current.CancellationToken);
        var afterUtc = DateTime.UtcNow.AddSeconds(2);

        var doc = new XmlDocument();
        doc.Load(Path.ChangeExtension(recordingPath, ".nfo"));
        var dateAddedText = doc.SelectSingleNode("//dateadded")?.InnerText ?? string.Empty;
        var parsed = DateTime.ParseExact(
            dateAddedText,
            "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture);

        Assert.InRange(parsed, beforeUtc, afterUtc);
    }

    [Fact]
    public async Task SaveRecordingMetadata_PropagatesCancellationToImageConversion()
    {
        Directory.CreateDirectory(_tempDir);
        var recordingPath = Path.Combine(_tempDir, "test-recording.ts");
        FileHelper.CreateEmpty(recordingPath);
        var sourceImagePath = Path.Combine(_tempDir, "source.jpg");
        File.WriteAllBytes(sourceImagePath, [1, 2, 3]);

        var config = new Mock<IConfigurationManager>();
        config.Setup(c => c.GetConfiguration("livetv"))
            .Returns(new LiveTvOptions { SaveRecordingNFO = false, SaveRecordingImages = true });
        config.Setup(c => c.GetConfiguration("xbmcmetadata"))
            .Returns(new XbmcMetadataOptions());

        var remoteImage = new ItemImageInfo
        {
            Path = "https://images.example.test/poster.jpg",
            Type = ImageType.Primary
        };
        var convertedImage = new ItemImageInfo
        {
            Path = sourceImagePath,
            Type = ImageType.Primary
        };
        var program = new LiveTvProgram();
        program.SetImage(remoteImage, 0);

        var cancellationToken = new CancellationTokenSource().Token;
        var libraryManager = new Mock<ILibraryManager>(MockBehavior.Strict);
        libraryManager
            .Setup(l => l.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns([program]);
        libraryManager
            .Setup(l => l.ConvertImageToLocal(program, remoteImage, 0, true, cancellationToken))
            .ReturnsAsync(convertedImage);

        var manager = new RecordingsMetadataManager(
            NullLogger<RecordingsMetadataManager>.Instance,
            config.Object,
            libraryManager.Object);

        var timer = new TimerInfo { Name = "Test Recording", ProgramId = "program" };

        await manager.SaveRecordingMetadata(timer, recordingPath, null, cancellationToken);

        libraryManager.Verify(
            l => l.ConvertImageToLocal(program, remoteImage, 0, true, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task SaveRecordingMetadata_StopsWhenCanceledDuringVideoNfoGeneration()
    {
        Directory.CreateDirectory(_tempDir);
        var recordingPath = Path.Combine(_tempDir, "test-recording.ts");
        FileHelper.CreateEmpty(recordingPath);

        using var cancellationTokenSource = new CancellationTokenSource();
        var config = new Mock<IConfigurationManager>();
        config.Setup(c => c.GetConfiguration("livetv"))
            .Returns(new LiveTvOptions { SaveRecordingNFO = true, SaveRecordingImages = false });
        config.Setup(c => c.GetConfiguration("xbmcmetadata"))
            .Callback(() => cancellationTokenSource.Cancel())
            .Returns(new XbmcMetadataOptions());

        var program = new LiveTvProgram();
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(l => l.GetItemList(It.IsAny<InternalItemsQuery>()))
            .Returns([program]);

        var manager = new RecordingsMetadataManager(
            NullLogger<RecordingsMetadataManager>.Instance,
            config.Object,
            libraryManager.Object);

        var timer = new TimerInfo { Name = "Test Recording", ProgramId = "program", IsMovie = true };

        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.SaveRecordingMetadata(
            timer,
            recordingPath,
            null,
            cancellationTokenSource.Token));
    }

    [Fact]
    public void SaveRecordingMetadata_PreservesThreeArgumentMethod()
    {
        Assert.NotNull(typeof(RecordingsMetadataManager).GetMethod(
            nameof(RecordingsMetadataManager.SaveRecordingMetadata),
            [typeof(TimerInfo), typeof(string), typeof(string)]));
    }
}

