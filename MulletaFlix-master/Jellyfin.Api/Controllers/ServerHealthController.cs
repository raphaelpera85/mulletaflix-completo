using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.SystemBackupService;
using MediaBrowser.Model.Nebula;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.System;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Server Health Controller - aggregates health metrics for dashboard.
/// </summary>
[Authorize(Policy = Policies.RequiresElevation)]
[ApiController]
[Route("ServerHealth")]
public class ServerHealthController : BaseMulletaFlixApiController
{
    private readonly IServerApplicationHost _applicationHost;
    private readonly IServerApplicationPaths _applicationPaths;
    private readonly IServerConfigurationManager _configurationManager;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ITaskManager _taskManager;
    private readonly ILibraryManager _libraryManager;
    private readonly IPluginManager _pluginManager;
    private readonly IBackupService _backupService;
    private readonly ISystemManager _systemManager;
    private readonly INebulaFtpManager _nebulaFtpManager;
    private readonly IMediaEncoder? _mediaEncoder;
    private readonly ITranscodeManager? _transcodeManager;

    public ServerHealthController(
        IServerApplicationHost applicationHost,
        IServerApplicationPaths applicationPaths,
        IServerConfigurationManager configurationManager,
        IHostApplicationLifetime applicationLifetime,
        ITaskManager taskManager,
        ILibraryManager libraryManager,
        IPluginManager pluginManager,
        IBackupService backupService,
        ISystemManager systemManager,
        INebulaFtpManager nebulaFtpManager,
        IMediaEncoder? mediaEncoder = null,
        ITranscodeManager? transcodeManager = null)
    {
        _applicationHost = applicationHost;
        _applicationPaths = applicationPaths;
        _configurationManager = configurationManager;
        _applicationLifetime = applicationLifetime;
        _taskManager = taskManager;
        _libraryManager = libraryManager;
        _pluginManager = pluginManager;
        _backupService = backupService;
        _systemManager = systemManager;
        _nebulaFtpManager = nebulaFtpManager;
        _mediaEncoder = mediaEncoder;
        _transcodeManager = transcodeManager;
    }

    /// <summary>
    /// Gets comprehensive server health summary.
    /// </summary>
    /// <response code="200">Health summary returned.</response>
    [HttpGet("Summary")]
    [ProducesResponseType(typeof(ServerHealthSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ServerHealthSummaryDto>> GetHealthSummary()
    {
        var summary = new ServerHealthSummaryDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            ServerName = _applicationHost.FriendlyName,
            Version = _applicationHost.ApplicationVersionString,
            HasPendingRestart = _applicationHost.HasPendingRestart,
            IsShuttingDown = _applicationLifetime.ApplicationStopping.IsCancellationRequested
        };

        // Storage health
        summary.Storage = await GetStorageHealthAsync().ConfigureAwait(false);

        // Task health
        summary.Tasks = GetTaskHealth();

        // Plugin health
        summary.Plugins = GetPluginHealth();

        // Backup health
        summary.Backup = await GetBackupHealthAsync().ConfigureAwait(false);

        // System health
        summary.System = GetSystemHealth();

        // Encoding health (lightweight diagnostic check without process execution delay)
        summary.Encoding = await GetEncodingHealthAsync(CancellationToken.None, runExecutionTest: false).ConfigureAwait(false);

        // Overall status
        summary.OverallStatus = CalculateOverallStatus(summary);

        return Ok(summary);
    }

    /// <summary>
    /// Gets actionable operational alerts: stalled upload queue, overdue backup, low disk space,
    /// repeated provider errors and restore failures. Deterministic evaluation over already-aggregated,
    /// low-cardinality signals; see <see cref="OperationalAlertEvaluator"/>.
    /// </summary>
    /// <response code="200">Alerts returned (possibly empty).</response>
    [HttpGet("Alerts")]
    [ProducesResponseType(typeof(IReadOnlyList<OperationalAlertDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<OperationalAlertDto>>> GetOperationalAlerts(CancellationToken cancellationToken)
    {
        var inputs = new OperationalAlertInputs
        {
            NowUtc = DateTime.UtcNow
        };

        // Queue-stall signal: the oldest pending/queued Nebula upload, when available.
        try
        {
            var queueSummary = await _nebulaFtpManager.GetUploadQueueSummaryAsync(cancellationToken).ConfigureAwait(false);
            if (queueSummary.IsAvailable)
            {
                inputs.OldestPendingUploadAtUtc = queueSummary.OldestPendingAtUtc;
                inputs.RecentFailuresByStage = queueSummary.FailuresByStage;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Nebula is an optional integration; a failure to read its queue summary must not block
            // the rest of the operational alerts (disk space, backup, restore remain evaluable).
        }

        // Backup-overdue and restore-failure signals from local backups and Supabase status.
        try
        {
            var backups = await _backupService.EnumerateBackups().ConfigureAwait(false);
            var lastSuccessfulBackup = backups.Where(b => b.Options.Database == true).OrderByDescending(b => b.DateCreated).FirstOrDefault();
            inputs.LastSuccessfulBackupUtc = lastSuccessfulBackup?.DateCreated.UtcDateTime;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Missing/unreadable local backups leave LastSuccessfulBackupUtc null, which itself
            // raises the critical "no successful backup" alert; no separate handling needed.
        }

        try
        {
            var supabaseStatus = await _nebulaFtpManager.GetSupabaseStatusAsync(cancellationToken).ConfigureAwait(false);
            if (supabaseStatus.IsConfigured && (inputs.LastSuccessfulBackupUtc is null || supabaseStatus.LastBackupTime > inputs.LastSuccessfulBackupUtc))
            {
                inputs.LastSuccessfulBackupUtc = supabaseStatus.LastBackupTime;
            }

            inputs.LastRestoreFailed = supabaseStatus.LastRestoreFailed;
            inputs.LastRestoreFailureMessage = supabaseStatus.LastRestoreFailed ? supabaseStatus.LastRestoreStatus : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Supabase is an optional remote sync target; unreachable status must not block alerts
            // derived from local signals (queue, disk, local backup).
        }

        // Disk-space signal from already-computed folder storage info.
        try
        {
            var storageInfo = _systemManager.GetSystemStorageInfo();
            var roles = new Dictionary<string, double>();
            AddStorageRole(roles, "cache", storageInfo.CacheFolder);
            AddStorageRole(roles, "backup", storageInfo.ProgramDataFolder);
            AddStorageRole(roles, "transcode", storageInfo.TranscodingTempFolder);
            AddStorageRole(roles, "log", storageInfo.LogFolder);
            inputs.StorageFreeRatioByRole = roles;
        }
        catch (Exception)
        {
            // Leave StorageFreeRatioByRole empty; disk-space alerts simply do not fire for this call.
        }

        var alerts = OperationalAlertEvaluator.Evaluate(inputs);
        return Ok(alerts);
    }

    private static void AddStorageRole(Dictionary<string, double> roles, string role, FolderStorageInfo? folder)
    {
        if (folder is null)
        {
            return;
        }

        var total = folder.FreeSpace + folder.UsedSpace;
        if (total <= 0)
        {
            return;
        }

        roles[role] = (double)folder.FreeSpace / total;
    }

    private async Task<StorageHealthDto> GetStorageHealthAsync()
    {
        var storageInfo = _systemManager.GetSystemStorageInfo();

        var folders = new[]
        {
            storageInfo.ProgramDataFolder,
            storageInfo.WebFolder,
            storageInfo.LogFolder,
            storageInfo.ImageCacheFolder,
            storageInfo.CacheFolder,
            storageInfo.InternalMetadataFolder,
            storageInfo.TranscodingTempFolder
        };

        var libraryFolders = storageInfo.Libraries.SelectMany(l => l.Folders).ToArray();
        var allFolders = folders.Concat(libraryFolders).Where(f => f != null).ToArray();

        var criticalFolders = allFolders.Where(f => f.FreeSpace > 0 && (double)f.FreeSpace / (f.FreeSpace + f.UsedSpace) < 0.1).ToArray();
        var warningFolders = allFolders.Where(f => f.FreeSpace > 0 && (double)f.FreeSpace / (f.FreeSpace + f.UsedSpace) < 0.2).Except(criticalFolders).ToArray();

        return new StorageHealthDto
        {
            TotalFreeSpace = allFolders.Where(f => f.FreeSpace > 0).Sum(f => f.FreeSpace),
            TotalUsedSpace = allFolders.Where(f => f.FreeSpace > 0).Sum(f => f.UsedSpace),
            CriticalCount = criticalFolders.Length,
            WarningCount = warningFolders.Length,
            HealthyCount = allFolders.Length - criticalFolders.Length - warningFolders.Length,
            CriticalPaths = criticalFolders.Select(f => f.Path).ToArray(),
            WarningPaths = warningFolders.Select(f => f.Path).ToArray(),
            Status = criticalFolders.Length > 0 ? HealthStatus.Critical : warningFolders.Length > 0 ? HealthStatus.Warning : HealthStatus.Ok
        };
    }

    private TaskHealthDto GetTaskHealth()
    {
        var taskWorkers = _taskManager.ScheduledTasks.ToArray();
        var failedTasks = taskWorkers.Where(t => t.LastExecutionResult?.Status == TaskCompletionStatus.Failed).ToArray();
        var runningTasks = taskWorkers.Where(t => t.State == TaskState.Running).ToArray();
        // Note: NextTrigger is not available in TaskTriggerInfo DTO, overdue check would require accessing trigger implementations
        var overdueTasks = Array.Empty<IScheduledTaskWorker>();

        return new TaskHealthDto
        {
            TotalCount = taskWorkers.Length,
            RunningCount = runningTasks.Length,
            FailedCount = failedTasks.Length,
            OverdueCount = overdueTasks.Length,
            FailedTaskNames = failedTasks.Select(t => t.Name).ToArray(),
            OverdueTaskNames = overdueTasks.Select(t => t.Name).ToArray(),
            RunningTaskNames = runningTasks.Select(t => t.Name).ToArray(),
            Status = failedTasks.Length > 0 ? HealthStatus.Critical : overdueTasks.Length > 0 ? HealthStatus.Warning : HealthStatus.Ok
        };
    }

    private PluginHealthDto GetPluginHealth()
    {
        var plugins = _pluginManager.Plugins.ToArray();
        var incompatiblePlugins = plugins.Where(p => p.Manifest != null &&
            p.Manifest.TargetAbi != null &&
            !IsCompatible(p.Manifest.TargetAbi)).ToArray();
        var updateAvailable = plugins.Where(p => HasUpdateAvailable(p)).ToArray();
        var disabledPlugins = plugins.Where(p => !p.IsEnabledAndSupported && p.Manifest.Status == PluginStatus.Disabled).ToArray();

        return new PluginHealthDto
        {
            TotalCount = plugins.Length,
            EnabledCount = plugins.Count(p => p.IsEnabledAndSupported),
            DisabledCount = disabledPlugins.Length,
            IncompatibleCount = incompatiblePlugins.Length,
            UpdateAvailableCount = updateAvailable.Length,
            IncompatibleNames = incompatiblePlugins.Select(p => p.Name).ToArray(),
            UpdateAvailableNames = updateAvailable.Select(p => p.Name).ToArray(),
            Status = incompatiblePlugins.Length > 0 ? HealthStatus.Critical : updateAvailable.Length > 0 ? HealthStatus.Warning : HealthStatus.Ok
        };
    }

    private async Task<BackupHealthDto> GetBackupHealthAsync()
    {
        var backups = await _backupService.EnumerateBackups().ConfigureAwait(false);
        var lastBackup = backups.OrderByDescending(b => b.DateCreated).FirstOrDefault();
        var lastSuccessfulBackup = backups.Where(b => b.Options.Database == true).OrderByDescending(b => b.DateCreated).FirstOrDefault();

        return new BackupHealthDto
        {
            TotalBackups = backups.Length,
            LastBackupTime = lastBackup?.DateCreated,
            LastSuccessfulBackupTime = lastSuccessfulBackup?.DateCreated,
            LastBackupResult = lastBackup?.Options?.Database == true ? TaskCompletionStatus.Completed : TaskCompletionStatus.Failed,
            LastBackupSize = lastBackup?.Options?.Database == true ? "Includes DB" : "Config only",
            Status = lastSuccessfulBackup == null ? HealthStatus.Critical :
                (DateTimeOffset.UtcNow - lastSuccessfulBackup.DateCreated).TotalDays > 7 ? HealthStatus.Warning : HealthStatus.Ok
        };
    }

    private SystemHealthDto GetSystemHealth()
    {
        var hasUpdateAvailable = false; // TODO: check update service
        var startupWizardCompleted = _configurationManager.CommonConfiguration.IsStartupWizardCompleted;

        return new SystemHealthDto
        {
            HasPendingRestart = _applicationHost.HasPendingRestart,
            HasUpdateAvailable = hasUpdateAvailable,
            StartupWizardCompleted = startupWizardCompleted,
            Status = _applicationHost.HasPendingRestart ? HealthStatus.Warning : HealthStatus.Ok
        };
    }

    private bool IsCompatible(string targetAbi)
    {
        // Simplified - would need proper version comparison
        return true;
    }

    private bool HasUpdateAvailable(MediaBrowser.Common.Plugins.LocalPlugin plugin)
    {
        // Simplified - would check catalog
        return false;
    }

    /// <summary>
    /// Gets FFmpeg/FFprobe and host hardware encoding diagnostics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="test">Whether to run an active execution test (default: true).</param>
    /// <response code="200">Encoding diagnostics returned.</response>
    [HttpGet("Encoding")]
    [ProducesResponseType(typeof(EncodingHealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EncodingHealthDto>> GetEncodingHealth(
        CancellationToken cancellationToken,
        [FromQuery] bool test = true)
    {
        var encodingHealth = await GetEncodingHealthAsync(cancellationToken, test).ConfigureAwait(false);
        return Ok(encodingHealth);
    }

    private async Task<EncodingHealthDto> GetEncodingHealthAsync(CancellationToken cancellationToken, bool runExecutionTest)
    {
        var encodingOptions = _configurationManager.GetEncodingOptions();
        var configuredHw = encodingOptions?.HardwareAccelerationType.ToString() ?? "none";

        var dto = new EncodingHealthDto
        {
            ConfiguredHwAcceleration = configuredHw,
            ActiveTranscodingJobsCount = _transcodeManager?.ActiveTranscodingJobsCount ?? 0,
            MaxConcurrentTranscodingJobs = encodingOptions?.MaxConcurrentTranscodingJobs ?? 0,
            HostCapabilities = DetectHostHardwareCapabilities()
        };

        if (_mediaEncoder == null)
        {
            dto.IsAvailable = false;
            dto.CanExecute = false;
            dto.ExecutionTestMessage = "Media encoder service is not registered.";
            dto.Status = HealthStatus.Critical;
            return dto;
        }

        dto.EncoderPath = _mediaEncoder.EncoderPath ?? string.Empty;
        dto.ProbePath = _mediaEncoder.ProbePath ?? string.Empty;
        dto.Version = _mediaEncoder.EncoderVersion?.ToString() ?? string.Empty;

        var fileExists = !string.IsNullOrWhiteSpace(dto.EncoderPath) && System.IO.File.Exists(dto.EncoderPath);
        dto.IsAvailable = !string.IsNullOrWhiteSpace(dto.EncoderPath) && _mediaEncoder.EncoderVersion != null;

        var candidateHwAccels = new[] { "d3d11va", "dxva2", "qsv", "cuda", "nvenc", "vaapi", "videotoolbox", "v4l2m2m", "rkmpp", "amf" };
        dto.SupportedHwAccelerations = candidateHwAccels.Where(h => _mediaEncoder.SupportsHwaccel(h)).ToArray();

        var candidateEncoders = new[]
        {
            "libx264", "libx265", "libsvtav1", "h264_nvenc", "hevc_nvenc", "av1_nvenc",
            "h264_qsv", "hevc_qsv", "av1_qsv", "h264_amf", "hevc_amf", "av1_amf",
            "h264_vaapi", "hevc_vaapi", "av1_vaapi", "aac", "libmp3lame", "libopus", "ac3", "flac"
        };
        dto.SupportedEncoders = candidateEncoders.Where(e => _mediaEncoder.SupportsEncoder(e)).ToArray();

        var candidateDecoders = new[]
        {
            "h264", "hevc", "vp8", "vp9", "av1", "mpeg2video", "vc1", "aac", "mp3", "ac3", "eac3", "flac", "truehd", "dca",
            "h264_qsv", "hevc_qsv", "av1_qsv", "h264_cuvid", "hevc_cuvid", "av1_cuvid"
        };
        dto.SupportedDecoders = candidateDecoders.Where(d => _mediaEncoder.SupportsDecoder(d)).ToArray();

        var candidateFilters = new[]
        {
            "scale", "scale_vaapi", "scale_qsv", "scale_cuda", "tonemap", "tonemap_opencl",
            "tonemap_vaapi", "overlay", "overlay_vaapi", "overlay_qsv", "overlay_cuda", "yadif",
            "deinterlace_qsv", "deinterlace_vaapi"
        };
        dto.SupportedFilters = candidateFilters.Where(f => _mediaEncoder.SupportsFilter(f)).ToArray();

        if (runExecutionTest)
        {
            if (!fileExists)
            {
                dto.CanExecute = false;
                dto.ExecutionTestMessage = "FFmpeg executable was not found on path.";
            }
            else
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    using var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = dto.EncoderPath,
                            Arguments = "-version",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();

                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    cts.CancelAfter(TimeSpan.FromSeconds(5));

                    await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                    sw.Stop();

                    dto.ExecutionTestDurationMs = sw.ElapsedMilliseconds;

                    if (process.ExitCode == 0)
                    {
                        dto.CanExecute = true;
                        var firstLine = (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false)) ?? string.Empty;
                        dto.ExecutionTestMessage = $"Execution successful (exit 0): {firstLine.Trim()}";
                    }
                    else
                    {
                        dto.CanExecute = false;
                        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                        dto.ExecutionTestMessage = $"Execution failed (exit {process.ExitCode}): {stderr.Trim()}";
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    dto.CanExecute = false;
                    dto.ExecutionTestMessage = $"Execution test exception: {ex.Message}";
                }
            }
        }
        else
        {
            dto.CanExecute = fileExists;
            dto.ExecutionTestMessage = fileExists ? "Executable found (execution test skipped)." : "Executable not found.";
        }

        if (!dto.IsAvailable || (runExecutionTest && !dto.CanExecute))
        {
            dto.Status = HealthStatus.Critical;
        }
        else if (configuredHw != "none" && !dto.SupportedHwAccelerations.Any(a => string.Equals(a, configuredHw, StringComparison.OrdinalIgnoreCase)))
        {
            dto.Status = HealthStatus.Warning;
        }
        else
        {
            dto.Status = HealthStatus.Ok;
        }

        return dto;
    }

    private HostHardwareCapabilitiesDto DetectHostHardwareCapabilities()
    {
        var isWindows = OperatingSystem.IsWindows();
        var isLinux = OperatingSystem.IsLinux();
        var isMacOS = OperatingSystem.IsMacOS();

        return new HostHardwareCapabilitiesDto
        {
            OperatingSystem = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            SupportsD3D11VA = isWindows && (_mediaEncoder?.SupportsHwaccel("d3d11va") ?? false),
            SupportsDxva2 = isWindows && (_mediaEncoder?.SupportsHwaccel("dxva2") ?? false),
            SupportsVaapi = (isLinux || OperatingSystem.IsFreeBSD()) && (_mediaEncoder?.SupportsHwaccel("vaapi") ?? false),
            SupportsQuickSync = _mediaEncoder?.SupportsHwaccel("qsv") ?? false,
            SupportsNvenc = (_mediaEncoder?.SupportsHwaccel("cuda") ?? false) || (_mediaEncoder?.SupportsEncoder("h264_nvenc") ?? false),
            SupportsAmf = isWindows && ((_mediaEncoder?.SupportsHwaccel("amf") ?? false) || (_mediaEncoder?.SupportsEncoder("h264_amf") ?? false)),
            SupportsVideoToolbox = isMacOS && (_mediaEncoder?.SupportsHwaccel("videotoolbox") ?? false),
            IsVaapiDeviceAmd = _mediaEncoder?.IsVaapiDeviceAmd ?? false,
            IsVaapiDeviceInteliHD = _mediaEncoder?.IsVaapiDeviceInteliHD ?? false,
            IsVaapiDeviceInteli965 = _mediaEncoder?.IsVaapiDeviceInteli965 ?? false,
            IsVaapiDeviceSupportVulkanDrmModifier = _mediaEncoder?.IsVaapiDeviceSupportVulkanDrmModifier ?? false,
            IsVaapiDeviceSupportVulkanDrmInterop = _mediaEncoder?.IsVaapiDeviceSupportVulkanDrmInterop ?? false
        };
    }

    private HealthStatus CalculateOverallStatus(ServerHealthSummaryDto summary)
    {
        var statuses = new List<HealthStatus>
        {
            summary.Storage.Status,
            summary.Tasks.Status,
            summary.Plugins.Status,
            summary.Backup.Status,
            summary.System.Status
        };

        if (summary.Encoding != null)
        {
            statuses.Add(summary.Encoding.Status);
        }

        if (statuses.Any(s => s == HealthStatus.Critical)) return HealthStatus.Critical;
        if (statuses.Any(s => s == HealthStatus.Warning)) return HealthStatus.Warning;
        return HealthStatus.Ok;
    }
}

/// <summary>
/// Health status enum.
/// </summary>
public enum HealthStatus
{
    Ok,
    Warning,
    Critical
}

/// <summary>
/// Complete server health summary.
/// </summary>
public class ServerHealthSummaryDto
{
    public DateTimeOffset Timestamp { get; set; }
    public string ServerName { get; set; }
    public string Version { get; set; }
    public bool HasPendingRestart { get; set; }
    public bool IsShuttingDown { get; set; }
    public StorageHealthDto Storage { get; set; }
    public TaskHealthDto Tasks { get; set; }
    public PluginHealthDto Plugins { get; set; }
    public BackupHealthDto Backup { get; set; }
    public SystemHealthDto System { get; set; }
    public EncodingHealthDto? Encoding { get; set; }
    public HealthStatus OverallStatus { get; set; }
}

/// <summary>
/// Storage health metrics.
/// </summary>
public class StorageHealthDto
{
    public long TotalFreeSpace { get; set; }
    public long TotalUsedSpace { get; set; }
    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int HealthyCount { get; set; }
    public string[] CriticalPaths { get; set; } = Array.Empty<string>();
    public string[] WarningPaths { get; set; } = Array.Empty<string>();
    public HealthStatus Status { get; set; }
}

/// <summary>
/// Scheduled task health metrics.
/// </summary>
public class TaskHealthDto
{
    public int TotalCount { get; set; }
    public int RunningCount { get; set; }
    public int FailedCount { get; set; }
    public int OverdueCount { get; set; }
    public string[] FailedTaskNames { get; set; } = Array.Empty<string>();
    public string[] OverdueTaskNames { get; set; } = Array.Empty<string>();
    public string[] RunningTaskNames { get; set; } = Array.Empty<string>();
    public HealthStatus Status { get; set; }
}

/// <summary>
/// Plugin health metrics.
/// </summary>
public class PluginHealthDto
{
    public int TotalCount { get; set; }
    public int EnabledCount { get; set; }
    public int DisabledCount { get; set; }
    public int IncompatibleCount { get; set; }
    public int UpdateAvailableCount { get; set; }
    public string[] IncompatibleNames { get; set; } = Array.Empty<string>();
    public string[] UpdateAvailableNames { get; set; } = Array.Empty<string>();
    public HealthStatus Status { get; set; }
}

/// <summary>
/// Backup health metrics.
/// </summary>
public class BackupHealthDto
{
    public int TotalBackups { get; set; }
    public DateTimeOffset? LastBackupTime { get; set; }
    public DateTimeOffset? LastSuccessfulBackupTime { get; set; }
    public TaskCompletionStatus? LastBackupResult { get; set; }
    public string LastBackupSize { get; set; }
    public HealthStatus Status { get; set; }
}

/// <summary>
/// System health metrics.
/// </summary>
public class SystemHealthDto
{
    public bool HasPendingRestart { get; set; }
    public bool HasUpdateAvailable { get; set; }
    public bool StartupWizardCompleted { get; set; }
    public HealthStatus Status { get; set; }
}

/// <summary>
/// FFmpeg/FFprobe encoding and host hardware capabilities health metrics.
/// </summary>
public class EncodingHealthDto
{
    public string EncoderPath { get; set; } = string.Empty;
    public string ProbePath { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool CanExecute { get; set; }
    public string? ExecutionTestMessage { get; set; }
    public long? ExecutionTestDurationMs { get; set; }
    public string ConfiguredHwAcceleration { get; set; } = string.Empty;
    public int ActiveTranscodingJobsCount { get; set; }
    public int MaxConcurrentTranscodingJobs { get; set; }
    public string[] SupportedHwAccelerations { get; set; } = Array.Empty<string>();
    public string[] SupportedEncoders { get; set; } = Array.Empty<string>();
    public string[] SupportedDecoders { get; set; } = Array.Empty<string>();
    public string[] SupportedFilters { get; set; } = Array.Empty<string>();
    public HostHardwareCapabilitiesDto HostCapabilities { get; set; } = new();
    public HealthStatus Status { get; set; }
}

/// <summary>
/// Host hardware capabilities for hardware-accelerated transcoding.
/// </summary>
public class HostHardwareCapabilitiesDto
{
    public string OperatingSystem { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public bool SupportsD3D11VA { get; set; }
    public bool SupportsDxva2 { get; set; }
    public bool SupportsVaapi { get; set; }
    public bool SupportsQuickSync { get; set; }
    public bool SupportsNvenc { get; set; }
    public bool SupportsAmf { get; set; }
    public bool SupportsVideoToolbox { get; set; }
    public bool IsVaapiDeviceAmd { get; set; }
    public bool IsVaapiDeviceInteliHD { get; set; }
    public bool IsVaapiDeviceInteli965 { get; set; }
    public bool IsVaapiDeviceSupportVulkanDrmModifier { get; set; }
    public bool IsVaapiDeviceSupportVulkanDrmInterop { get; set; }
}