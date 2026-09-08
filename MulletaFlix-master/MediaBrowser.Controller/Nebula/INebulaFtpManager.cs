using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Nebula;

namespace MediaBrowser.Controller.Nebula;

public interface INebulaFtpManager
{
    Task<NebulaStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    NebulaLogsDto GetLogs(int serverOffset, int downloaderOffset);

    Task<bool> StartEnvioAsync(bool streamOnly, CancellationToken cancellationToken = default);

    Task<bool> StopEnvioAsync(CancellationToken cancellationToken = default);

    Task<bool> MountDriveNAsync(CancellationToken cancellationToken = default);

    Task<bool> UnmountDriveNAsync(CancellationToken cancellationToken = default);

    Task<bool> StartDownloaderAsync(CancellationToken cancellationToken = default);

    Task<bool> StopDownloaderAsync(CancellationToken cancellationToken = default);

    Task<bool> GenerateStrmAsync(CancellationToken cancellationToken = default);

    Task<bool> PruneCompletedAsync(CancellationToken cancellationToken = default);

    System.Collections.Generic.List<NebulaBotDto> GetBots();

    System.Collections.Generic.List<NebulaBotDto> SaveBot(NebulaSaveBotRequest request);

    System.Collections.Generic.List<NebulaBotDto> DeleteBot(int index);

    System.Collections.Generic.List<NebulaBotDto> SyncBotsFromEnv();

    Task<NebulaSupabaseTestResponseDto> TestSupabaseConnectionAsync(NebulaSupabaseTestRequest? request, CancellationToken cancellationToken = default);

    Task<NebulaSupabaseStatusDto> GetSupabaseStatusAsync(CancellationToken cancellationToken = default);

    Task<NebulaSupabaseBackupResultDto> BackupMongoToSupabaseAsync(CancellationToken cancellationToken = default);

    Task<NebulaSupabaseRestoreResultDto> RestoreSupabaseToMongoAsync(CancellationToken cancellationToken = default);

    string GetSupabaseSqlScript();
}
