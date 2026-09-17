using System;

namespace MediaBrowser.Model.Configuration;

public class NebulaFtpConfiguration
{
    // Nebula is an optional integration. Keep new installations healthy until
    // the operator explicitly configures and enables its external services.
    public bool Enabled { get; set; }

    public string RaiDriveDownloadUrl { get; set; } = "https://www.raidrive.com/download";

    // Bind locally by default. Remote access must be an explicit operator decision.
    public string ServerHost { get; set; } = "127.0.0.1";

    public int ServerPort { get; set; } = 2121;

    public int HttpStreamPort { get; set; } = 2123;

    /// <summary>
    /// Gets or sets the token used to authorize native HTTP streaming and catalog requests.
    /// </summary>
    public string HttpStreamToken { get; set; } = string.Empty;

    public string PassivePorts { get; set; } = "60000-60009";

    public int MaxActiveConnections { get; set; } = 256;

    /// <summary>
    /// Gets or sets a value indicating whether plaintext FTP/HTTP may bind to a non-loopback host.
    /// FTPS is not available in the native server, so this is an explicit insecure opt-in.
    /// </summary>
    public bool AllowInsecureRemoteFtp { get; set; }

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

    /// <summary>
    /// Gets or sets a value indicating whether FTP fallback STRM URLs may embed credentials.
    /// Disabled by default because URLs can be copied to logs, history and metadata.
    /// </summary>
    public bool EmbedFtpCredentialsInStrmUrls { get; set; }

    public string DriveLetter { get; set; } = "N:";

    public string RemotePath { get; set; } = "/";

    public bool UseMappedDrive { get; set; } = true;

    public string WatchFolderPath { get; set; } = string.Empty;

    public string SetupNotes { get; set; } = "O NebulaFTP funciona sem montar unidade de rede. O modo Envio e Streaming operam via FTP/HTTP virtual. O servidor nativo não oferece FTPS; mantenha ServerHost em loopback ou habilite AllowInsecureRemoteFtp apenas em uma rede confiável. Use o botão Baixar RaiDrive apenas se precisar acessar via letra de drive (N:) para compatibilidade com players legados.";

    public string NebulaFolderPath { get; set; } = string.Empty;

    public string[] MonitorPaths { get; set; } = ["D:\\midias"];

    public string[] StagePaths { get; set; } = ["E:\\NebulaStage", "F:\\NebulaStage", "I:\\NebulaStage"];

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
