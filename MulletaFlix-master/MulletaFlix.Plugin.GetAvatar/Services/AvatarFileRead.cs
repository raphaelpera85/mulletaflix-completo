namespace MulletaFlix.Plugin.GetAvatar.Services;

/// <summary>
/// An avatar stream opened relative to the catalog directory handle.
/// </summary>
public sealed class AvatarFileRead : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AvatarFileRead"/> class.
    /// </summary>
    /// <param name="fileName">The catalog file name.</param>
    /// <param name="stream">The already-opened file stream.</param>
    public AvatarFileRead(string fileName, FileStream stream)
    {
        FileName = fileName;
        Stream = stream;
    }

    /// <summary>
    /// Gets the catalog file name.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the opened file stream.
    /// </summary>
    public FileStream Stream { get; }

    /// <inheritdoc />
    public void Dispose() => Stream.Dispose();
}
