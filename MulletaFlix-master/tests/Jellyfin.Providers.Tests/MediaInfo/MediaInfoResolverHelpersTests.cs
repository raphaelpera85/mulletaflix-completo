using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Emby.Naming.Common;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Providers.MediaInfo;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Providers.Tests.MediaInfo;

public partial class MediaInfoResolverTests
{
    private static MediaStream CreateMediaStream(string path, string? language, string? title, int index, bool isForced = false, bool isDefault = false, bool isHearingImpaired = false)
    {
        return new MediaStream
        {
            Index = index,
            Type = MediaStreamType.Subtitle,
            Path = path,
            IsDefault = isDefault,
            IsForced = isForced,
            IsHearingImpaired = isHearingImpaired,
            Language = language,
            Title = title
        };
    }

    /// <summary>
    /// Provides an <see cref="IDirectoryService"/> that when queried for the test video/metadata directory will return a path including the provided file name.
    /// </summary>
    /// <param name="file">The name of the file to locate.</param>
    /// <param name="useMetadataDirectory"><c>true</c> if the file belongs in the metadata directory.</param>
    /// <returns>A mocked <see cref="IDirectoryService"/>.</returns>
    public static IDirectoryService GetDirectoryServiceForExternalFile(string file, bool useMetadataDirectory = false)
    {
        var directoryService = new Mock<IDirectoryService>(MockBehavior.Strict);
        if (useMetadataDirectory)
        {
            directoryService.Setup(ds => ds.GetFilePaths(It.IsRegex(VideoDirectoryRegex), It.IsAny<bool>()))
                .Returns(Array.Empty<string>());
            directoryService.Setup(ds => ds.GetFilePaths(It.IsRegex(MetadataDirectoryRegex), It.IsAny<bool>()))
                .Returns(new[] { MetadataDirectoryPath + "/" + file });
        }
        else
        {
            directoryService.Setup(ds => ds.GetFilePaths(It.IsRegex(VideoDirectoryRegex), It.IsAny<bool>()))
                .Returns(new[] { VideoDirectoryPath + "/" + file });
            directoryService.Setup(ds => ds.GetFilePaths(It.IsRegex(MetadataDirectoryRegex), It.IsAny<bool>()))
                .Returns(Array.Empty<string>());
        }

        return directoryService.Object;
    }
}

