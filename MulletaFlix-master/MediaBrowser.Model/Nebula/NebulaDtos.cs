using System;
using System.Collections.Generic;

namespace MediaBrowser.Model.Nebula;

public class NebulaStatusDto
{
    public bool IsEnvioRunning { get; set; }

    public bool StreamOnly { get; set; }

    public bool IsDownloaderRunning { get; set; }

    public bool TurboActive { get; set; }

    public string GlobalStatusText { get; set; } = string.Empty;

    public bool IsDriveNMounted { get; set; }

    public string DriveNStatus { get; set; } = string.Empty;

    public List<NebulaWorkerItemDto> ActiveUploads { get; set; } = new();

    public List<NebulaWorkerItemDto> QueuedUploads { get; set; } = new();

    public int UploadQueueCount { get; set; }

    public NebulaDownloadStatusDto CurrentDownload { get; set; } = new();

    public List<NebulaStageDiskDto> StageDisks { get; set; } = new();

    public string StageDisksFormatted { get; set; } = string.Empty;

    public NebulaOperationStatusDto MaintenanceOperation { get; set; } = new();
}

public class NebulaOperationStatusDto
{
    public string Name { get; set; } = string.Empty;

    public string State { get; set; } = "idle";

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public long? DurationMs { get; set; }

    public string Error { get; set; } = string.Empty;

    public double? ProgressPercent { get; set; }

    public string ProgressText { get; set; } = string.Empty;
}

public class NebulaComponentHealthDto
{
    public bool MongoConfigured { get; set; }

    public bool MongoConnected { get; set; }

    public string MongoStatus { get; set; } = string.Empty;

    public bool TelegramConfigured { get; set; }

    public bool TelegramReady { get; set; }

    public int TelegramAvailableBots { get; set; }

    public bool FtpListenerRunning { get; set; }

    public bool HttpListenerRunning { get; set; }
}

public class NebulaCredentialRotationRequest
{
    public string? Password { get; set; }

    public string? HttpStreamToken { get; set; }

    public string? SupabaseKey { get; set; }

    public string? ApiHash { get; set; }
}

public class NebulaWorkerItemDto
{
    public string Name { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Status { get; set; } = "uploading";

    public string WorkerId { get; set; } = "?";

    public string BotText { get; set; } = string.Empty;

    public double Percentage { get; set; }

    public long Size { get; set; }

    public long UploadedBytes { get; set; }

    public string InfoText { get; set; } = string.Empty;
}

public class NebulaDownloadStatusDto
{
    public string Name { get; set; } = "Nenhum download em andamento";

    public string StageStep { get; set; } = "Aguardando início do Downloader...";

    public double Percentage { get; set; }

    public double DoneMb { get; set; }

    public double TotalMb { get; set; }

    public string Speed { get; set; } = string.Empty;

    public string DetailText { get; set; } = "0.0%";
}

public class NebulaStageDiskDto
{
    public string Path { get; set; } = string.Empty;

    public double FreeGb { get; set; }

    public double TotalGb { get; set; }

    public double FreePercent { get; set; }

    public string Formatted { get; set; } = string.Empty;
}

public class NebulaLogsDto
{
    public List<string> ServerLogs { get; set; } = new();

    public int ServerLogTotal { get; set; }

    public List<string> DownloaderLogs { get; set; } = new();

    public int DownloaderLogTotal { get; set; }
}

public class NebulaActionRequest
{
    public bool StreamOnly { get; set; }
}

public class NebulaBotDto
{
    public int Index { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string MaskedToken { get; set; } = string.Empty;

    public bool SessionExists { get; set; }

    public bool Enabled { get; set; } = true;
}

public class NebulaSaveBotRequest
{
    public int? Index { get; set; }

    public string Token { get; set; } = string.Empty;

    public string? Name { get; set; }
}

public class NebulaSupabaseStatusDto
{
    public bool IsConfigured { get; set; }

    public bool IsConnected { get; set; }

    public string SupabaseUrl { get; set; } = string.Empty;

    public bool HasKey { get; set; }

    public bool AutoBackupEnabled { get; set; } = true;

    public int AutoBackupIntervalHours { get; set; } = 6;

    public DateTime? LastBackupTime { get; set; }

    public string LastBackupStatus { get; set; } = string.Empty;

    public int TotalRemoteFiles { get; set; }

    public int TotalLocalFiles { get; set; }

    public string Message { get; set; } = string.Empty;
}

public class NebulaSupabaseTestRequest
{
    public string? Url { get; set; }

    public string? Key { get; set; }
}

public class NebulaSupabaseTestResponseDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public int StatusCode { get; set; }
}

public class NebulaSupabaseBackupResultDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public int FilesBackedUp { get; set; }

    public int UsersBackedUp { get; set; }

    public double ElapsedSeconds { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class NebulaSupabaseRestoreResultDto
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public int FilesRestored { get; set; }

    public int UsersRestored { get; set; }

    public double ElapsedSeconds { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
