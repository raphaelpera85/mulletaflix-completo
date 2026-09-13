using System;
using System.Linq;
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

    [HttpGet("Health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<NebulaComponentHealthDto>> GetHealth(CancellationToken cancellationToken)
    {
        var health = await _nebulaManager.GetComponentHealthAsync(cancellationToken).ConfigureAwait(false);
        return Ok(health);
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult UpdateConfig([FromBody] NebulaFtpConfiguration config)
    {
        if (config == null)
        {
            return BadRequest();
        }

        var validationError = ValidateConfiguration(config);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        var existing = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp") ?? new NebulaFtpConfiguration();
        PreserveExistingSecretValues(config, existing);
        _configManager.SaveConfiguration("nebulaftp", config);
        return NoContent();
    }

    [HttpPost("Config/Secrets")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult RotateSecrets([FromBody] NebulaCredentialRotationRequest? request)
    {
        if (request is null)
        {
            return BadRequest();
        }

        var values = new[] { request.Password, request.HttpStreamToken, request.SupabaseKey, request.ApiHash }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();
        if (values.Length == 0 || values.Any(value => value.Length > 4096))
        {
            return BadRequest("Informe ao menos um segredo novo com no máximo 4096 caracteres.");
        }

        if (!string.IsNullOrWhiteSpace(request.ApiHash) && request.ApiHash.Trim().Length != 32)
        {
            return BadRequest("ApiHash deve ter 32 caracteres.");
        }

        var existing = _configManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp") ?? new NebulaFtpConfiguration();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            existing.Password = request.Password.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.HttpStreamToken))
        {
            existing.HttpStreamToken = request.HttpStreamToken.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.SupabaseKey))
        {
            existing.SupabaseKey = request.SupabaseKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.ApiHash))
        {
            existing.ApiHash = request.ApiHash.Trim();
        }

        _configManager.SaveConfiguration("nebulaftp", existing);
        return NoContent();
    }

    private static string? ValidateConfiguration(NebulaFtpConfiguration config)
    {
        if (config.ServerPort is < 1 or > 65535)
        {
            return "ServerPort deve estar entre 1 e 65535.";
        }

        if (config.HttpStreamPort is < 1 or > 65535)
        {
            return "HttpStreamPort deve estar entre 1 e 65535.";
        }

        if (config.MaxActiveConnections is < 1 or > 4096)
        {
            return "MaxActiveConnections deve estar entre 1 e 4096.";
        }

        if (config.MaxWorkers is < 1 or > 128)
        {
            return "MaxWorkers deve estar entre 1 e 128.";
        }

        if (config.ChunkSizeMb is < 1 or > 1024)
        {
            return "ChunkSizeMb deve estar entre 1 e 1024 MB.";
        }

        if (!string.IsNullOrWhiteSpace(config.SupabaseUrl)
            && (!Uri.TryCreate(config.SupabaseUrl.Trim(), UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || string.IsNullOrWhiteSpace(uri.Host)
                || !string.IsNullOrEmpty(uri.UserInfo)))
        {
            return "SupabaseUrl deve ser uma URL HTTPS absoluta sem credenciais embutidas.";
        }

        return null;
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
        var ok = await _nebulaManager.GenerateStrmAsync(GetIdempotencyKey(), cancellationToken).ConfigureAwait(false);
        return Ok(ok);
    }

    [HttpPost("Actions/PruneCompleted")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> PruneCompleted(CancellationToken cancellationToken)
    {
        var ok = await _nebulaManager.PruneCompletedAsync(GetIdempotencyKey(), cancellationToken).ConfigureAwait(false);
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<System.Collections.Generic.List<NebulaBotDto>> SaveBot([FromBody] NebulaSaveBotRequest? request)
    {
        if (request is null)
        {
            return BadRequest();
        }

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
    public ActionResult<NebulaSupabaseBackupResultDto> BackupSupabase()
    {
        // Backups can take several minutes. Do not bind their lifetime to the
        // browser request: a closed tab/WebSocket or an HTTP timeout must not
        // cancel the database synchronization.
        var idempotencyKey = GetIdempotencyKey();
        _ = Task.Run(
            () => _nebulaManager.BackupMongoToSupabaseAsync(idempotencyKey, CancellationToken.None),
            CancellationToken.None);

        return Ok(new NebulaSupabaseBackupResultDto
        {
            Success = true,
            Message = "Backup iniciado em segundo plano. Acompanhe o progresso no status de manutenção.",
            Timestamp = DateTime.UtcNow
        });
    }

    [HttpPost("Supabase/Restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<NebulaSupabaseRestoreResultDto>> RestoreSupabase(CancellationToken cancellationToken)
    {
        var result = await _nebulaManager.RestoreSupabaseToMongoAsync(GetIdempotencyKey(), cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("Supabase/SqlScript")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<string> GetSupabaseSqlScript()
    {
        var script = _nebulaManager.GetSupabaseSqlScript();
        return Content(script, "text/plain; charset=utf-8");
    }

    private string? GetIdempotencyKey()
    {
        if (!Request.Headers.TryGetValue("X-Idempotency-Key", out var values))
        {
            return null;
        }

        var key = values.ToString().Trim();
        return key.Length is > 0 and <= 128 && System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z0-9._:-]+$")
            ? key
            : null;
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
            MaxActiveConnections = config.MaxActiveConnections,
            HttpStreamPort = config.HttpStreamPort,
            AllowInsecureRemoteFtp = config.AllowInsecureRemoteFtp,
            HttpStreamToken = string.Empty,
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
            EmbedFtpCredentialsInStrmUrls = config.EmbedFtpCredentialsInStrmUrls,
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

        if (string.IsNullOrWhiteSpace(config.HttpStreamToken))
        {
            config.HttpStreamToken = existing.HttpStreamToken;
        }

        if (string.IsNullOrWhiteSpace(config.SupabaseKey))
        {
            config.SupabaseKey = existing.SupabaseKey;
        }
    }
}
