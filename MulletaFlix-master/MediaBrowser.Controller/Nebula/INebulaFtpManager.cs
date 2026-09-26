using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Nebula;

namespace MediaBrowser.Controller.Nebula;

public interface INebulaFtpManager
{
    /// <summary>
    /// Dá prioridade máxima de download e upload para a mídia solicitada (e todas as temporadas/episódios se for série/animação/novela/drama).
    /// </summary>
    void PrioritizeMedia(string mediaPath, string? seriesPath = null, string? seriesName = null);

    /// <summary>
    /// Dá prioridade máxima de download e upload para a mídia informada pelo BaseItem do Jellyfin.
    /// Se o item for série, animação, novela ou drama (ou episódio/temporada pertencente a um deles),
    /// dá prioridade para todos os arquivos de temporadas e episódios da obra.
    /// </summary>
    void PrioritizeItem(BaseItem item);

    Task<NebulaStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<NebulaComponentHealthDto> GetComponentHealthAsync(CancellationToken cancellationToken = default);

    NebulaLogsDto GetLogs(int serverOffset, int downloaderOffset);

    Task<bool> StartEnvioAsync(bool streamOnly, CancellationToken cancellationToken = default);

    Task<bool> StopEnvioAsync(CancellationToken cancellationToken = default);

    Task<bool> MountDriveNAsync(CancellationToken cancellationToken = default);

    Task<bool> UnmountDriveNAsync(CancellationToken cancellationToken = default);

    Task<bool> StartDownloaderAsync(CancellationToken cancellationToken = default);

    /// <summary>Starts prefetching all Telegram chunks for the selected media in the background.</summary>
    Task<bool> StartPlaybackPrefetchAsync(string mediaPath, CancellationToken cancellationToken = default);

    /// <summary>Obtém informações e status de armazenamento do cache de reprodução de mídia.</summary>
    NebulaPlaybackCacheStatusDto GetPlaybackCacheStatus();

    /// <summary>Limpa os arquivos em cache que não estejam atualmente em reprodução.</summary>
    Task<bool> ClearPlaybackCacheAsync(CancellationToken cancellationToken = default);

    /// <summary>Atualiza o diretório de destino do cache de reprodução de mídia.</summary>
    Task<bool> UpdatePlaybackCachePathAsync(string newPath, CancellationToken cancellationToken = default);

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
