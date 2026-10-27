using System;
using System.IO;
using MediaBrowser.Common.Configuration;
using MulletaFlix.Server.Implementations.StorageHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.StorageHelpers;

public sealed class StorageHelperTests
{
    [Fact]
    public void TestCommonPathsForStorageCapacityReadOnly_DoesNotCreateConfiguredPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mulletaflix-storage-test-{Guid.NewGuid():N}");
        var paths = new Mock<IApplicationPaths>();
        paths.SetupGet(x => x.DataPath).Returns(Path.Combine(root, "data"));
        paths.SetupGet(x => x.CachePath).Returns(Path.Combine(root, "cache"));
        paths.SetupGet(x => x.ProgramDataPath).Returns(Path.Combine(root, "program"));

        StorageHelper.TestCommonPathsForStorageCapacityReadOnly(paths.Object, Mock.Of<ILogger>());

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void GetFreeSpaceOf_ResolvesAbsolutePathFromItsVolumeRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mulletaflix-storage-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        try
        {
            var storage = StorageHelper.GetFreeSpaceOf(path);

            Assert.Equal(Path.GetFullPath(path), storage.ResolvedPath, OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
            Assert.True(storage.FreeSpace > 0);
            Assert.False(string.IsNullOrWhiteSpace(storage.DeviceId));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
