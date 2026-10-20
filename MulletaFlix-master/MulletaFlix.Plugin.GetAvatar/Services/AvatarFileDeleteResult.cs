namespace MulletaFlix.Plugin.GetAvatar.Services;

/// <summary>
/// Describes the result of deleting an avatar catalog entry.
/// </summary>
public enum AvatarFileDeleteResult
{
    /// <summary>The entry was deleted.</summary>
    Deleted,

    /// <summary>The entry does not exist.</summary>
    NotFound,

    /// <summary>The entry is not a safe regular file.</summary>
    Unsafe
}
