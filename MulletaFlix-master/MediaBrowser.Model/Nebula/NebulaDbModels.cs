#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CA2227 // Collection properties should be read only

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MediaBrowser.Model.Nebula;

/// <summary>
/// Representa uma parte de um arquivo dividido no Telegram.
/// </summary>
public class NebulaFilePart
{
    [JsonPropertyName("part_number")]
    public int PartNumber { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("tg_file_id")]
    public string TgFileId { get; set; } = string.Empty;

    [JsonPropertyName("tg_message_id")]
    public long TgMessageId { get; set; }

    [JsonPropertyName("tg_chat_id")]
    public long TgChatId { get; set; }

    [JsonPropertyName("bot_index")]
    public int BotIndex { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("uploaded_at")]
    public double? UploadedAt { get; set; }
}

/// <summary>
/// Representa um nó de arquivo ou diretório no sistema de arquivos virtual do Nebula.
/// </summary>
public class NebulaFileNode
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("parent")]
    public string? Parent { get; set; }

    [JsonPropertyName("is_directory")]
    public bool IsDirectory { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed"; // completed, pending, uploading, failed

    [JsonPropertyName("parts")]
    public List<NebulaFilePart> Parts { get; set; } = [];

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("created_at")]
    public double CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [JsonPropertyName("modified_at")]
    public double ModifiedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [JsonPropertyName("uploaded_at")]
    public double? UploadedAt { get; set; }
}

/// <summary>
/// Representa um usuário autenticado no Nebula FTP.
/// </summary>
public class NebulaUser
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;

    [JsonPropertyName("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [JsonPropertyName("permissions")]
    public string Permissions { get; set; } = "elradfmwM";

    [JsonPropertyName("created_at")]
    public double CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [JsonPropertyName("last_login")]
    public double? LastLogin { get; set; }
}
