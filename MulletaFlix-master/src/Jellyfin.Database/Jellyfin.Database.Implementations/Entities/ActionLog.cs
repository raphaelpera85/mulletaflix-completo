using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MulletaFlix.Database.Implementations.Entities;

/// <summary>
/// Represents an administrative action log entry for audit trail.
/// </summary>
public class ActionLog
{
    public long Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string ActionType { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string EntityType { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? EntityId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [MaxLength(256)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;

    public string? Details { get; set; }

    [MaxLength(512)]
    public string? OldValues { get; set; }

    [MaxLength(512)]
    public string? NewValues { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    public bool IsSuccess { get; set; } = true;

    [MaxLength(1024)]
    public string? ErrorMessage { get; set; }

    [NotMapped]
    public ActionLogCategory Category { get; set; }

    public static ActionLog Create(
        string actionType,
        string entityType,
        string? entityId,
        Guid userId,
        string username,
        string? oldValues = null,
        string? newValues = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new ActionLog
        {
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            Username = username,
            OldValues = oldValues,
            NewValues = newValues,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DateCreated = DateTimeOffset.UtcNow,
            IsSuccess = true
        };
    }

    public static ActionLog CreateError(
        string actionType,
        string entityType,
        string? entityId,
        Guid userId,
        string username,
        string errorMessage,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new ActionLog
        {
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            Username = username,
            Details = details,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DateCreated = DateTimeOffset.UtcNow,
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
    }
}

public enum ActionLogCategory
{
    UserManagement,
    PluginManagement,
    BackupRestore,
    LibraryManagement,
    TaskManagement,
    SystemConfiguration,
    ScheduledTask,
    Authentication,
    PluginConfiguration,
    MetadataManagement
}