using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using MediaBrowser.Model.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MulletaFlix.Api.Controllers;

[Route("NebulaFtp")]
[Authorize(Policy = Policies.RequiresElevation)]
public sealed class NebulaFtpController : BaseMulletaFlixApiController
{
    private readonly INebulaFtpManager _nebulaManager;
    private readonly IServerConfigurationManager _configManager;

    public NebulaFtpController(
        INebulaFtpManager nebulaManager,
        IServerConfigurationManager configManager)
    {
        _nebulaManager = nebulaManager;
        _configManager = configManager;
    }

    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<NebulaStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await _nebulaManager.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return Ok(status);
    }

    [HttpGet("Logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<NebulaLogsDto> GetLogs([FromQuery] int serverOffset = 0, [FromQuery] int downloaderOffset = 0)
    {
        var logs = _nebulaManager.GetLogs(serverOffset, downloaderOffset);
        return Ok(logs);
    }

    [HttpGet("Config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<NebulaFtpConfiguration> GetConfig()
    {
        var config = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp") ?? new NebulaFtpConfiguration();
        return Ok(CreateSafeConfigResponse(config));
    }

    [HttpPost("Config")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult UpdateConfig([FromBody] NebulaFtpConfiguration config)
    {
        if (config == null)
        {
            return BadRequest();
        }

        var existing = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp") ?? new NebulaFtpConfiguration();
        PreserveExistingSecretValues(config, existing);
        _configManager.SaveConfiguration("nebulaftp", config);
        return NoContent();
    }

    [HttpPost("Actions/StartEnvio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> StartEnvio([FromBody] NebulaActionRequest? req, CancellationToken cancellationToken)
    {
        var streamOnly = req?.StreamOnly ?? false;
        var ok = await _nebulaManager.StartEnvioAsync(streamOnly, cancellationToken).ConfigureAwait(false);
        return Ok(ok);
    }

    [HttpPost("Actions/StopEnvio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> StopEnvio(CancellationToken cancellationToken)
    {
        var ok = await _nebulaManager.StopEnvioAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ok);
    }

    [HttpPost("Actions/MountDriveN")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> MountDriveN(CancellationToken cancellationToken)
    {
        var ok = await _nebulaManager.MountDriveNAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ok);
    }

    [HttpPost("Actions/UnmountDriveN")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> UnmountDriveN(CancellationToken cancellationToken)
    {
        var ok = await _nebulaManager.UnmountDriveNAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ok);
    }

    [HttpPost("Actions/StartDownloader")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> StartDownloader(CancellationToken cancellationToken)
    {
        var ok = await _nebulaManager.StartDownloaderAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ok);
    }

    [HttpPost("Actions/StopDownloader")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> StopDownloader(CancellationToken cancellationToken)
    {
        var ok = await _nebulaManager.StopDownloaderAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ok);
    }

    [HttpPost("Actions/GenerateStrm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> GenerateStrm(CancellationToken cancellationToken)
    {
        var ok = await _nebulaManager.GenerateStrmAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ok);
    }

    [HttpPost("Actions/PruneCompleted")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> PruneCompleted(CancellationToken cancellationToken)
    {
        var ok = await _nebulaManager.PruneCompletedAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ok);
    }

    [HttpGet("Bots")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<System.Collections.Generic.List<NebulaBotDto>> GetBots()
    {
        var bots = _nebulaManager.GetBots();
        return Ok(bots);
    }

    [HttpPost("Bots")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<System.Collections.Generic.List<NebulaBotDto>> SaveBot([FromBody] NebulaSaveBotRequest request)
    {
        var bots = _nebulaManager.SaveBot(request);
        return Ok(bots);
    }

    [HttpDelete("Bots/{index}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<System.Collections.Generic.List<NebulaBotDto>> DeleteBot([FromRoute] int index)
    {
        var bots = _nebulaManager.DeleteBot(index);
        return Ok(bots);
    }

    [HttpPost("Bots/Sync")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<System.Collections.Generic.List<NebulaBotDto>> SyncBots()
    {
        var bots = _nebulaManager.SyncBotsFromEnv();
        return Ok(bots);
    }

    [HttpGet("Supabase/Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<NebulaSupabaseStatusDto>> GetSupabaseStatus(CancellationToken cancellationToken)
    {
        var status = await _nebulaManager.GetSupabaseStatusAsync(cancellationToken).ConfigureAwait(false);
        return Ok(status);
    }

    [HttpPost("Supabase/Test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<NebulaSupabaseTestResponseDto>> TestSupabase([FromBody] NebulaSupabaseTestRequest? request, CancellationToken cancellationToken)
    {
        var result = await _nebulaManager.TestSupabaseConnectionAsync(request, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("Supabase/Backup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<NebulaSupabaseBackupResultDto>> BackupSupabase(CancellationToken cancellationToken)
    {
        var result = await _nebulaManager.BackupMongoToSupabaseAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpPost("Supabase/Restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<NebulaSupabaseRestoreResultDto>> RestoreSupabase(CancellationToken cancellationToken)
    {
        var result = await _nebulaManager.RestoreSupabaseToMongoAsync(cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("Supabase/SqlScript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<string> GetSupabaseSqlScript()
    {
        var script = _nebulaManager.GetSupabaseSqlScript();
        return Content(script, "text/plain; charset=utf-8");
    }

    private static NebulaFtpConfiguration CreateSafeConfigResponse(NebulaFtpConfiguration config)
    {
        return new NebulaFtpConfiguration
        {
            Enabled = config.Enabled,
            RaiDriveDownloadUrl = config.RaiDriveDownloadUrl,
            ServerHost = config.ServerHost,
            ServerPort = config.ServerPort,
            PassivePorts = config.PassivePorts,
            MongoDbConnectionString = string.Empty,
            ApiId = config.ApiId,
            ApiHash = string.Empty,
            ChatId = config.ChatId,
            BotTokens = string.Empty,
            BotTokensCollection = config.BotTokensCollection,
            BotTokensTable = config.BotTokensTable,
            MaxWorkers = config.MaxWorkers,
            ChunkSizeMb = config.ChunkSizeMb,
            DeleteSourceAfterUpload = config.DeleteSourceAfterUpload,
            Username = config.Username,
            Password = string.Empty,
            DriveLetter = config.DriveLetter,
            RemotePath = config.RemotePath,
            UseMappedDrive = config.UseMappedDrive,
            WatchFolderPath = config.WatchFolderPath,
            SetupNotes = config.SetupNotes,
            NebulaFolderPath = config.NebulaFolderPath,
            MonitorPaths = config.MonitorPaths,
            StagePaths = config.StagePaths,
            TurboEnabled = config.TurboEnabled,
            TurboIdleMinutes = config.TurboIdleMinutes,
            DownloadParts = config.DownloadParts,
            SupabaseUrl = config.SupabaseUrl,
            SupabaseKey = string.Empty,
            SupabaseAutoBackup = config.SupabaseAutoBackup,
            SupabaseAutoBackupIntervalHours = config.SupabaseAutoBackupIntervalHours,
            SupabaseLastBackupTime = config.SupabaseLastBackupTime,
            SupabaseLastBackupStatus = config.SupabaseLastBackupStatus,
            SupabaseLastBackupFilesCount = config.SupabaseLastBackupFilesCount
        };
    }

    private static void PreserveExistingSecretValues(NebulaFtpConfiguration config, NebulaFtpConfiguration existing)
    {
        if (string.IsNullOrWhiteSpace(config.MongoDbConnectionString))
        {
            config.MongoDbConnectionString = existing.MongoDbConnectionString;
        }

        if (string.IsNullOrWhiteSpace(config.ApiHash))
        {
            config.ApiHash = existing.ApiHash;
        }

        if (string.IsNullOrWhiteSpace(config.BotTokens))
        {
            config.BotTokens = existing.BotTokens;
        }

        if (string.IsNullOrWhiteSpace(config.Password))
        {
            config.Password = existing.Password;
        }

        if (string.IsNullOrWhiteSpace(config.SupabaseKey))
        {
            config.SupabaseKey = existing.SupabaseKey;
        }
    }
}
