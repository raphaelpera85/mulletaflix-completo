using MediaBrowser.Model.System;

namespace MulletaFlix.Server.Implementations.StorageHelpers;

/// <summary>
/// Provides information about available storage space.
/// </summary>
public interface IStorageSpaceProvider
{
    /// <summary>
    /// Gets the free space of the filesystem containing the specified path.
    /// </summary>
    /// <param name="path">A path on the filesystem to inspect.</param>
    /// <returns>Storage information for the filesystem containing the path.</returns>
    FolderStorageInfo GetFreeSpaceOf(string path);
}
