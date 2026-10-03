using System.IO;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.XbmcMetadata.Savers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.XbmcMetadata.Tests.Location;

public class BookNfoLocationTests
{
    [Fact]
    public void GetSavePath_UsesSameFolderAndBookFilename()
    {
        var saver = new BookNfoSaver(null!, null!, null!, null!, null!, NullLogger<BookNfoSaver>.Instance);
        var book = new Book { Path = Path.Combine("library", "fantasy", "Cityscape.epub") };

        var savePath = saver.GetSavePath(book);

        Assert.Equal(Path.Combine("library", "fantasy", "Cityscape.nfo"), savePath);
    }
}
