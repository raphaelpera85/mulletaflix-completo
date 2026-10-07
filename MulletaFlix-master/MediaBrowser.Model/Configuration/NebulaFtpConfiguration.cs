using System;

namespace MediaBrowser.Model.Configuration;

public class NebulaFtpConfiguration
{
    /// <summary>Creates a shallow snapshot for scalar configuration/result updates; collections must not be mutated.</summary>
    /// <returns>A separate configuration instance.</returns>
    public NebulaFtpConfiguration CreateSnapshot() => (NebulaFtpConfiguration)MemberwiseClone();

    /// <summary>Copies server-owned outcomes from the current configuration before replacing editable settings.</summary>
    /// <param name="existing">The current configuration inside the persistence gate.</param>
    public void PreserveBackupHistory(NebulaFtpConfiguration existing)
    {
        SupabaseLastBackupTime = existing.SupabaseLastBackupTime;
        SupabaseLastBackupStatus = existing.SupabaseLastBackupStatus;
        SupabaseLastBackupFilesCount = existing.SupabaseLastBackupFilesCount;
        SupabaseLastBackupAttemptTime = existing.SupabaseLastBackupAttemptTime;
        SupabaseLastBackupFailed = existing.SupabaseLastBackupFailed;
        SupabaseLastBackupProcessedFilesCount = existing.SupabaseLastBackupProcessedFilesCount;
        SupabaseLastBackupProcessedUsersCount = existing.SupabaseLastBackupProcessedUsersCount;
        SupabaseLastUsersBackupTime = existing.SupabaseLastUsersBackupTime;
        SupabaseLastUsersBackupStatus = existing.SupabaseLastUsersBackupStatus;
        SupabaseLastUsersBackupCount = existing.SupabaseLastUsersBackupCount;
        SupabaseLastUsersBackupFailed = existing.SupabaseLastUsersBackupFailed;
        SupabaseLastRestoreTime = existing.SupabaseLastRestoreTime;
        SupabaseLastRestoreStatus = existing.SupabaseLastRestoreStatus;
        SupabaseLastRestoreFailed = existing.SupabaseLastRestoreFailed;
        SupabaseLastRestoreFilesRestored = existing.SupabaseLastRestoreFilesRestored;
        SupabaseLastRestoreUsersRestored = existing.SupabaseLastRestoreUsersRestored;
        SupabaseLastRestoreFtpUsersRestored = existing.SupabaseLastRestoreFtpUsersRestored;
        SupabaseLastRestoreAppUsersRestored = existing.SupabaseLastRestoreAppUsersRestored;
    }

    public const int DefaultSupabaseAutoBackupIntervalHours = 1;
    public const int DefaultSupabaseUsersBackupIntervalHours = 24;

    // Nebula is an optional integration. Keep new installations healthy until
    // the operator explicitly configures and enables its external services.
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the full Python runtime (aioftp, Pyrogram multi-bot, strm_downloader)
    /// should be used as the primary engine for high performance transfers.
    /// </summary>
    public bool UsePythonEngine { get; set; } = true;

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

    public bool TelegramNotificationsEnabled { get; set; } = true;

    public string TelegramNotificationChatIds { get; set; } = string.Empty;

    public int TelegramNotificationIntervalSeconds { get; set; } = 3;

    /// <summary>Configuração genérica da camada Notifications; Telegram é o transporte atual.</summary>
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>IDs de canais/chats separados por vírgula para a camada Notifications.</summary>
    public string NotificationsChannelIds { get; set; } = string.Empty;

    public int NotificationsIntervalSeconds { get; set; } = 3;

    /// <summary>URL pública usada nos links enviados nas notificações.</summary>
    public string PublicServerUrl { get; set; } = "http://mulletaflix.duckdns.org:8096";

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

    /// <summary>
    /// Gets or sets the custom cache path for Nebula media playback chunks.
    /// When empty, defaults to the server's cache path.
    /// </summary>
    public string PlaybackCachePath { get; set; } = string.Empty;

    /// <summary>Maximum Nebula playback cache size in GiB.</summary>
    public int PlaybackCacheMaxSizeGb { get; set; } = 50;

    /// <summary>Minimum free disk space to preserve while caching, in GiB.</summary>
    public int PlaybackCacheMinimumFreeSpaceGb { get; set; } = 2;

    public string[] MonitorPaths { get; set; } = ["D:\\midias"];

    /// <summary>Gets or sets media titles submitted through the request form that should remain prioritized across Nebula restarts.</summary>
    public string[] RequestedMediaPriorities { get; set; } = Array.Empty<string>();

    public string[] StagePaths { get; set; } = ["E:\\NebulaStage", "F:\\NebulaStage", "I:\\NebulaStage"];

    /// <summary>
    /// Gets or sets a ordem de download preferida das categorias de mídia (ex: ANIMACAO, FILME,
    /// SERIE, DORAMA, NOVELA, PORNO), definida pelo operador arrastando cartões na interface.
    /// Vazio usa a ordem padrão de fábrica. Ver <see cref="Jellyfin.Server.Implementations.Nebula.NebulaCategoryOrder"/>.
    /// </summary>
    public string[] CategoryDownloadOrder { get; set; } = Array.Empty<string>();

    public bool TurboEnabled { get; set; } = true;

    public int TurboIdleMinutes { get; set; } = 10;

    public int DownloadParts { get; set; } = 32;

    public string SupabaseUrl { get; set; } = "https://potnzsdhnjoxfzfcvmzk.supabase.co";

    public string SupabaseKey { get; set; } = string.Empty;

    /// <summary>Identificador do projeto usado pela Management API durante o provisionamento inicial.</summary>
    public string SupabaseProjectRef { get; set; } = string.Empty;

    /// <summary>Token temporário da Management API; é removido após o schema ser provisionado.</summary>
    public string SupabaseManagementToken { get; set; } = string.Empty;

    public bool SupabaseAutoBackup { get; set; } = true;

    public int SupabaseAutoBackupIntervalHours { get; set; } = DefaultSupabaseAutoBackupIntervalHours;

    public DateTime? SupabaseLastBackupTime { get; set; }

    public string SupabaseLastBackupStatus { get; set; } = "Nenhum backup realizado ainda";

    public int SupabaseLastBackupFilesCount { get; set; }

    public DateTime? SupabaseLastBackupAttemptTime { get; set; }

    public bool? SupabaseLastBackupFailed { get; set; }

    public int? SupabaseLastBackupProcessedFilesCount { get; set; }

    public int? SupabaseLastBackupProcessedUsersCount { get; set; }

    /// <summary>Last independent MulletaFlix-user backup time.</summary>
    public DateTime? SupabaseLastUsersBackupTime { get; set; }

    /// <summary>Last independent MulletaFlix-user backup result.</summary>
    public string SupabaseLastUsersBackupStatus { get; set; } = "Aguardando primeiro backup automático";

    /// <summary>Number of application users included in the last independent backup.</summary>
    public int SupabaseLastUsersBackupCount { get; set; }

    public bool? SupabaseLastUsersBackupFailed { get; set; }

    /// <summary>Last Supabase-to-MongoDB restore time (Nebula media/users), for operational alerting.</summary>
    public DateTime? SupabaseLastRestoreTime { get; set; }

    /// <summary>Last Supabase-to-MongoDB restore result text, for operational alerting.</summary>
    public string SupabaseLastRestoreStatus { get; set; } = "Nenhuma restauração realizada ainda";

    /// <summary>Whether the last Supabase-to-MongoDB restore attempt failed; drives the restore-failure alert.</summary>
    public bool SupabaseLastRestoreFailed { get; set; }

    /// <summary>Number of Nebula catalog files restored by the last Supabase-to-MongoDB restore attempt; null when unknown.</summary>
    public int? SupabaseLastRestoreFilesRestored { get; set; }

    /// <summary>Number of Nebula FTP and MulletaFlix users restored by the last Supabase restore attempt; null when unknown.</summary>
    public int? SupabaseLastRestoreUsersRestored { get; set; }

    /// <summary>Number of FTP users restored by the last Supabase restore attempt; null when unknown.</summary>
    public int? SupabaseLastRestoreFtpUsersRestored { get; set; }

    /// <summary>Number of MulletaFlix application users restored by the last Supabase restore attempt; null when unknown.</summary>
    public int? SupabaseLastRestoreAppUsersRestored { get; set; }
}
