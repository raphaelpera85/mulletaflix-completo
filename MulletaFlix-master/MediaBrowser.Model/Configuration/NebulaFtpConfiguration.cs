using System;

namespace MediaBrowser.Model.Configuration;

public class NebulaFtpConfiguration
{
    public bool Enabled { get; set; } = true;

    public string RaiDriveDownloadUrl { get; set; } = "https://www.raidrive.com/download";

    public string ServerHost { get; set; } = "0.0.0.0";

    public int ServerPort { get; set; } = 2121;

    public string PassivePorts { get; set; } = "60000-60009";

    public string MongoDbConnectionString { get; set; } = "mongodb://localhost:27017";

    public string ApiId { get; set; } = "38735893";

    public string ApiHash { get; set; } = string.Empty;

    public string ChatId { get; set; } = "-1004391811380";

    public string BotTokens { get; set; } = string.Empty;

    public string BotTokensCollection { get; set; } = "bot_tokens";

    public string BotTokensTable { get; set; } = "nebula_bot_tokens";

    public int MaxWorkers { get; set; } = 10;

    public int ChunkSizeMb { get; set; } = 64;

    public bool DeleteSourceAfterUpload { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string DriveLetter { get; set; } = "N:";

    public string RemotePath { get; set; } = "/";

    public bool UseMappedDrive { get; set; } = true;

    public string WatchFolderPath { get; set; } = string.Empty;

    public string SetupNotes { get; set; } = "Use o botao Baixar RaiDrive para montar o NebulaFTP como unidade no Windows.";

    public string NebulaFolderPath { get; set; } = string.Empty;

    public string[] MonitorPaths { get; set; } = ["D:\\midias"];

    public string[] StagePaths { get; set; } = ["E:\\NebulaStage", "I:\\NebulaStage"];

    public bool TurboEnabled { get; set; } = true;

    public int TurboIdleMinutes { get; set; } = 10;

    public int DownloadParts { get; set; } = 32;

    public string SupabaseUrl { get; set; } = "https://potnzsdhnjoxfzfcvmzk.supabase.co";

    public string SupabaseKey { get; set; } = string.Empty;

    public bool SupabaseAutoBackup { get; set; } = true;

    public int SupabaseAutoBackupIntervalHours { get; set; } = 6;

    public DateTime? SupabaseLastBackupTime { get; set; }

    public string SupabaseLastBackupStatus { get; set; } = "Nenhum backup realizado ainda";

    public int SupabaseLastBackupFilesCount { get; set; }
}
