using MediaBrowser.Model.Configuration;

namespace MulletaFlix.Api.Helpers;

/// <summary>Preserves server-owned execution outcomes when clients replace editable configuration.</summary>
internal static class NebulaConfigurationHistory
{
    internal static void Preserve(NebulaFtpConfiguration config, NebulaFtpConfiguration existing)
    {
        config.SupabaseLastBackupTime = existing.SupabaseLastBackupTime;
        config.SupabaseLastBackupStatus = existing.SupabaseLastBackupStatus;
        config.SupabaseLastBackupFilesCount = existing.SupabaseLastBackupFilesCount;
        config.SupabaseLastBackupAttemptTime = existing.SupabaseLastBackupAttemptTime;
        config.SupabaseLastBackupFailed = existing.SupabaseLastBackupFailed;
        config.SupabaseLastBackupProcessedFilesCount = existing.SupabaseLastBackupProcessedFilesCount;
        config.SupabaseLastBackupProcessedUsersCount = existing.SupabaseLastBackupProcessedUsersCount;
        config.SupabaseLastUsersBackupTime = existing.SupabaseLastUsersBackupTime;
        config.SupabaseLastUsersBackupStatus = existing.SupabaseLastUsersBackupStatus;
        config.SupabaseLastUsersBackupCount = existing.SupabaseLastUsersBackupCount;
        config.SupabaseLastUsersBackupFailed = existing.SupabaseLastUsersBackupFailed;
        config.SupabaseLastRestoreTime = existing.SupabaseLastRestoreTime;
        config.SupabaseLastRestoreStatus = existing.SupabaseLastRestoreStatus;
        config.SupabaseLastRestoreFailed = existing.SupabaseLastRestoreFailed;
    }
}
