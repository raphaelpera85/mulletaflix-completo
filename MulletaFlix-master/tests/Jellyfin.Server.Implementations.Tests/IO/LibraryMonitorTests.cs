using System;
using System.Linq;
using Emby.Server.Implementations.IO;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.IO;

public class LibraryMonitorTests
{
    [Fact]
    public void GetConfiguredLibraryPaths_UsesOnlyEnabledVirtualFolderLocations()
    {
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetVirtualFolders())
            .Returns(
            [
                new VirtualFolderInfo
                {
                    Locations = [@"D:\strm_library\Animações", @"F:\NebulaStage\Filmes"],
                    LibraryOptions = new LibraryOptions { EnableRealtimeMonitor = true }
                },
                new VirtualFolderInfo
                {
                    Locations = [@"N:\Animações", @"N:\Series"],
                    LibraryOptions = new LibraryOptions { EnableRealtimeMonitor = false }
                }
            ]);

        var paths = LibraryMonitor.GetConfiguredLibraryPaths(libraryManager.Object).ToArray();

        Assert.Equal([@"D:\strm_library\Animações", @"F:\NebulaStage\Filmes"], paths);
        Assert.DoesNotContain(paths, path => path.StartsWith(@"N:\", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetConfiguredLibraryPaths_RemovesCaseInsensitiveDuplicatesAndBlankLocations()
    {
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetVirtualFolders())
            .Returns(
            [
                new VirtualFolderInfo
                {
                    Locations = [@"E:\NebulaStage\Filmes\", @"e:\nebulastage\filmes", "", "   "],
                    LibraryOptions = new LibraryOptions { EnableRealtimeMonitor = true }
                },
                new VirtualFolderInfo
                {
                    Locations = [@"N:\Filmes"],
                    LibraryOptions = null
                }
            ]);

        var paths = LibraryMonitor.GetConfiguredLibraryPaths(libraryManager.Object).ToArray();

        Assert.Single(paths);
        Assert.Equal(@"E:\NebulaStage\Filmes", paths[0]);
    }
}
