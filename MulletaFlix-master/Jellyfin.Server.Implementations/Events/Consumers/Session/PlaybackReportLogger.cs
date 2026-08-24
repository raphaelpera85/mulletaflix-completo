using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Logging;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Activity;

namespace MulletaFlix.Server.Implementations.Events.Consumers.Session;

/// <summary>
/// Creates a playback report entry whenever a user starts/stops playback.
/// </summary>
public class PlaybackReportLogger : IEventConsumer<PlaybackStartEventArgs>, IEventConsumer<PlaybackStopEventArgs>, IEventConsumer<PlaybackProgressEventArgs>
{
    private readonly ILogger<PlaybackReportLogger> _logger;
    private readonly IPlaybackReportManager _playbackReportManager;
    private readonly ConcurrentDictionary<string, PlaybackReport> _activeReports = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaybackReportLogger"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="playbackReportManager">The playback report manager.</param>
    public PlaybackReportLogger(ILogger<PlaybackReportLogger> logger, IPlaybackReportManager playbackReportManager)
    {
        _logger = logger;
        _playbackReportManager = playbackReportManager;
    }

    /// <inheritdoc />
    public async Task OnEvent(PlaybackStartEventArgs eventArgs)
    {
        if (eventArgs.MediaInfo is null)
        {
            _logger.LogWarning("PlaybackStart reported with null media info.");
            return;
        }

        if (eventArgs.Item is not null && eventArgs.Item.IsThemeMedia)
        {
            // Don't report theme song or local trailer playback
            return;
        }

        if (eventArgs.Users.Count == 0)
        {
            return;
        }

        var user = eventArgs.Users[0];
        var key = GetSessionKey(eventArgs);

        var report = new PlaybackReport(
            user.Id,
            eventArgs.Item?.Id ?? Guid.Empty,
            eventArgs.MediaInfo.Name,
            eventArgs.MediaInfo.MediaType.ToString(),
            eventArgs.DeviceId,
            eventArgs.DeviceName ?? "Unknown",
            eventArgs.ClientName ?? "Unknown",
            eventArgs.PlaySessionId ?? Guid.NewGuid().ToString("N"),
            eventArgs.Session?.Id ?? Guid.NewGuid().ToString("N"))
        {
            SeriesName = eventArgs.MediaInfo.SeriesName,
            SeasonNumber = eventArgs.MediaInfo.ParentIndexNumber,
            EpisodeNumber = eventArgs.MediaInfo.IndexNumber,
            Artist = eventArgs.MediaInfo.Artists?.FirstOrDefault() ?? string.Empty,
            Album = eventArgs.MediaInfo.Album,
            StartTimeUtc = DateTime.UtcNow,
            StartPositionTicks = eventArgs.PlaybackPositionTicks,
            ItemRuntimeTicks = eventArgs.MediaInfo.RunTimeTicks,
            SessionId = eventArgs.Session?.Id ?? "",
            RemoteEndPoint = eventArgs.Session?.RemoteEndPoint,
            IsLocal = IsLocalPlayback(eventArgs.Session),
            LibraryId = eventArgs.Item?.ParentId
        };

        _activeReports[key] = report;

        await _playbackReportManager.CreateAsync(report).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task OnEvent(PlaybackProgressEventArgs eventArgs)
    {
        if (eventArgs.MediaInfo is null)
        {
            return;
        }

        if (eventArgs.Item is not null && eventArgs.Item.IsThemeMedia)
        {
            return;
        }

        if (eventArgs.Users.Count == 0)
        {
            return;
        }

        var key = GetSessionKey(eventArgs);

        if (_activeReports.TryGetValue(key, out var report))
        {
            // Update progress
            report.EndPositionTicks = eventArgs.PlaybackPositionTicks;
            report.EndTimeUtc = DateTime.UtcNow;

            if (report.StartPositionTicks.HasValue && report.EndPositionTicks.HasValue)
            {
                report.DurationSeconds = (report.EndPositionTicks.Value - report.StartPositionTicks.Value) / 10000000;
            }

            if (report.ItemRuntimeTicks.HasValue && report.EndPositionTicks.HasValue)
            {
                report.CompletionPercentage = (double)report.EndPositionTicks.Value / report.ItemRuntimeTicks.Value * 100;
            }
        }
    }

    /// <inheritdoc />
    public async Task OnEvent(PlaybackStopEventArgs eventArgs)
    {
        var item = eventArgs.MediaInfo;

        if (item is null)
        {
            _logger.LogWarning("PlaybackStopped reported with null media info.");
            return;
        }

        if (eventArgs.Item is not null && eventArgs.Item.IsThemeMedia)
        {
            return;
        }

        if (eventArgs.Users.Count == 0)
        {
            return;
        }

        var key = GetSessionKey(eventArgs);

        if (_activeReports.TryRemove(key, out var report))
        {
            report.EndTimeUtc = DateTime.UtcNow;
            report.PlayedToCompletion = eventArgs.PlayedToCompletion;

            if (report.StartPositionTicks.HasValue && report.EndPositionTicks.HasValue)
            {
                report.DurationSeconds = (report.EndPositionTicks.Value - report.StartPositionTicks.Value) / 10000000;
            }

            if (report.ItemRuntimeTicks.HasValue && report.EndPositionTicks.HasValue)
            {
                report.CompletionPercentage = (double)report.EndPositionTicks.Value / report.ItemRuntimeTicks.Value * 100;
            }

            await _playbackReportManager.UpdateAsync(report).ConfigureAwait(false);
        }
        else
        {
            // If no start report was found, create a basic one from stop event
            var user = eventArgs.Users[0];
            var newReport = new PlaybackReport(
                user.Id,
                eventArgs.Item?.Id ?? Guid.Empty,
                item.Name,
                item.MediaType.ToString(),
                eventArgs.DeviceId,
                eventArgs.DeviceName ?? "Unknown",
                eventArgs.ClientName ?? "Unknown",
                eventArgs.PlaySessionId ?? Guid.NewGuid().ToString("N"),
                eventArgs.Session?.Id ?? Guid.NewGuid().ToString("N"))
            {
                SeriesName = item.SeriesName,
                SeasonNumber = item.ParentIndexNumber,
                EpisodeNumber = item.IndexNumber,
                Artist = item.Artists?.FirstOrDefault(),
                Album = item.Album,
                StartTimeUtc = DateTime.UtcNow.AddMinutes(-5), // Estimate
                EndTimeUtc = DateTime.UtcNow,
                ItemRuntimeTicks = item.RunTimeTicks,
                EndPositionTicks = eventArgs.PlaybackPositionTicks,
                PlayedToCompletion = eventArgs.PlayedToCompletion,
                SessionId = eventArgs.Session?.Id ?? "",
                RemoteEndPoint = eventArgs.Session?.RemoteEndPoint,
                IsLocal = IsLocalPlayback(eventArgs.Session),
                LibraryId = eventArgs.Item?.ParentId
            };

            if (newReport.EndPositionTicks.HasValue && newReport.ItemRuntimeTicks.HasValue)
            {
                newReport.CompletionPercentage = (double)newReport.EndPositionTicks.Value / newReport.ItemRuntimeTicks.Value * 100;
            }

            await _playbackReportManager.CreateAsync(newReport).ConfigureAwait(false);
        }
    }

    private static string GetSessionKey(PlaybackProgressEventArgs eventArgs)
    {
        return $"{eventArgs.PlaySessionId}_{eventArgs.Session?.Id}_{eventArgs.DeviceId}";
    }

    private static bool IsLocalPlayback(SessionInfo? session)
    {
        if (session == null) return false;

        // Check if the remote endpoint is a local IP
        if (!string.IsNullOrEmpty(session.RemoteEndPoint))
        {
            var remoteIp = session.RemoteEndPoint.Split(':')[0];
            return remoteIp == "127.0.0.1" ||
                   remoteIp.StartsWith("192.168.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("10.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.16.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.17.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.18.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.19.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.20.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.21.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.22.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.23.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.24.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.25.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.26.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.27.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.28.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.29.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.30.", StringComparison.Ordinal) ||
                   remoteIp.StartsWith("172.31.", StringComparison.Ordinal);
        }

        return false;
    }
}