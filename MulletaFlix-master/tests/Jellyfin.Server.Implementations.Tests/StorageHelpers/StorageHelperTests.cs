using System;
using System.IO;
using MulletaFlix.Server.Implementations.StorageHelpers;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.StorageHelpers;

public sealed class StorageHelperTests
{
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
