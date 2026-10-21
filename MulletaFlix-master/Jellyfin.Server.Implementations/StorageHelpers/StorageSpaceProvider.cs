using MediaBrowser.Model.System;

namespace MulletaFlix.Server.Implementations.StorageHelpers;

/// <summary>
/// Reads filesystem capacity using the platform storage helper.
/// </summary>
public sealed class StorageSpaceProvider : IStorageSpaceProvider
{
    /// <inheritdoc />
    public FolderStorageInfo GetFreeSpaceOf(string path) => StorageHelper.GetFreeSpaceOf(path);
}
