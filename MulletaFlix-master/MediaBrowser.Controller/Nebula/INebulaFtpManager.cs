using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Nebula;

namespace MediaBrowser.Controller.Nebula;

public interface INebulaFtpManager
{
    Task<NebulaStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<NebulaComponentHealthDto> GetComponentHealthAsync(CancellationToken cancellationToken = default);

    NebulaLogsDto GetLogs(int serverOffset, int downloaderOffset);

    Task<bool> StartEnvioAsync(bool streamOnly, CancellationToken cancellationToken = default);

    Task<bool> StopEnvioAsync(CancellationToken cancellationToken = default);

    Task<bool> MountDriveNAsync(CancellationToken cancellationToken = default);

    Task<bool> UnmountDriveNAsync(CancellationToken cancellationToken = default);

    Task<bool> StartDownloaderAsync(CancellationToken cancellationToken = default);

    Task<bool> StopDownloaderAsync(CancellationToken cancellationToken = default);

    Task<bool> GenerateStrmAsync(string? idempotencyKey = null, CancellationToken cancellationToken = default);

    Task<bool> PruneCompletedAsync(string? idempotencyKey = null, CancellationToken cancellationToken = default);

    Task<NebulaNovelaMigrationResult> ScanAndMoveNovelasAsync(CancellationToken cancellationToken = default);

    Task<NebulaAnimacaoMigrationResult> NormalizeAnimacoesLibraryAsync(CancellationToken cancellationToken = default);

    System.Collections.Generic.List<NebulaBotDto> GetBots();

    System.Collections.Generic.List<NebulaBotDto> SaveBot(NebulaSaveBotRequest request);

    System.Collections.Generic.List<NebulaBotDto> DeleteBot(int index);

    System.Collections.Generic.List<NebulaBotDto> SyncBotsFromEnv();

    Task<NebulaSupabaseTestResponseDto> TestSupabaseConnectionAsync(NebulaSupabaseTestRequest? request, CancellationToken cancellationToken = default);

    Task<NebulaSupabaseProvisionResultDto> ProvisionSupabaseSchemaAsync(NebulaSupabaseProvisionRequest? request, CancellationToken cancellationToken = default);

    Task<NebulaSupabaseStatusDto> GetSupabaseStatusAsync(CancellationToken cancellationToken = default);

    Task<NebulaSupabaseBackupResultDto> BackupMongoToSupabaseAsync(string? idempotencyKey = null, bool forceFull = false, CancellationToken cancellationToken = default);

    Task<NebulaSupabaseBackupResultDto> BackupUsersToSupabaseAsync(CancellationToken cancellationToken = default);

    Task<NebulaSupabaseRestoreResultDto> RestoreSupabaseToMongoAsync(string? idempotencyKey = null, bool forceFull = false, CancellationToken cancellationToken = default);

    Task<NebulaSupabaseRestoreResultDto> RestoreUsersFromSupabaseAsync(CancellationToken cancellationToken = default);

    string GetSupabaseSqlScript();

    /// <summary>
    /// Envia uma notificação formatada para o canal ou chat do Telegram.
    /// </summary>
    /// <param name="messageHtml">Mensagem em formato HTML.</param>
    /// <param name="targetChatId">Chat ID de destino opcional.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Verdadeiro se entregue com sucesso.</returns>
    Task<bool> SendTelegramNotificationAsync(string messageHtml, string? targetChatId = null, CancellationToken cancellationToken = default);

    NebulaTelegramNotificationSettingsDto GetTelegramNotificationSettings();

    bool SaveTelegramNotificationSettings(NebulaTelegramNotificationSettingsRequest request);

    Task<bool> SendNotificationAsync(string messageHtml, string? imagePath = null, string? targetChannelId = null, CancellationToken cancellationToken = default);

    NebulaNotificationsSettingsDto GetNotificationsSettings();

    bool SaveNotificationsSettings(NebulaNotificationsSettingsRequest request);
}
