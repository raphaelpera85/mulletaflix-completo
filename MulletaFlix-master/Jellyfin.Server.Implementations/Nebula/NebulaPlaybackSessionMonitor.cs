using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>Cancels speculative Nebula prefetch when playback sessions finish or switch media.</summary>
public sealed class NebulaPlaybackSessionMonitor : IHostedService
{
    private readonly ISessionManager _sessionManager;
    private readonly INebulaFtpManager _nebulaManager;
    private readonly ILogger<NebulaPlaybackSessionMonitor> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, ActivePlayback> _sessions = new(StringComparer.Ordinal);

    public NebulaPlaybackSessionMonitor(
        ISessionManager sessionManager,
        INebulaFtpManager nebulaManager,
        ILogger<NebulaPlaybackSessionMonitor> logger)
    {
        _sessionManager = sessionManager;
        _nebulaManager = nebulaManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        lock (_gate)
        {
            _sessions.Clear();
        }

        return Task.CompletedTask;
    }

    internal async Task TrackPlaybackStartAsync(string? sessionId, string? playSessionId, string? mediaPath)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        string? pathToCancel = null;
        lock (_gate)
        {
            if (_sessions.Remove(sessionId, out var previous)
                && !PathsEqual(previous.MediaPath, mediaPath)
                && !_sessions.Values.Any(active => PathsEqual(active.MediaPath, previous.MediaPath)))
            {
                pathToCancel = previous.MediaPath;
            }

            if (!string.IsNullOrWhiteSpace(mediaPath))
            {
                _sessions[sessionId] = new ActivePlayback(mediaPath, playSessionId);
            }
        }

        await CancelIfUnusedAsync(pathToCancel).ConfigureAwait(false);
    }

    internal async Task TrackPlaybackStoppedAsync(string? sessionId, string? playSessionId, string? mediaPath = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        string? pathToCancel = null;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var active))
            {
                return;
            }

            var hasPlaySessionIdentity = !string.IsNullOrWhiteSpace(active.PlaySessionId)
                || !string.IsNullOrWhiteSpace(playSessionId);
            if (hasPlaySessionIdentity
                    ? !string.Equals(active.PlaySessionId, playSessionId, StringComparison.Ordinal)
                    : string.IsNullOrWhiteSpace(mediaPath)
                        || !PathsEqual(active.MediaPath, mediaPath))
            {
                return;
            }

            _sessions.Remove(sessionId);
            if (!_sessions.Values.Any(other => PathsEqual(other.MediaPath, active.MediaPath)))
            {
                pathToCancel = active.MediaPath;
            }
        }

        await CancelIfUnusedAsync(pathToCancel).ConfigureAwait(false);
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs args)
    {
        _ = TrackPlaybackStartAsync(args.Session?.Id, args.PlaySessionId, args.Item?.Path);
    }

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs args)
    {
        _ = TrackPlaybackStoppedAsync(args.Session?.Id, args.PlaySessionId, args.Item?.Path);
    }

    private async Task CancelIfUnusedAsync(string? mediaPath)
    {
        if (string.IsNullOrWhiteSpace(mediaPath))
        {
            return;
        }

        try
        {
            await _nebulaManager.CancelPlaybackPrefetchAsync(mediaPath, CancellationToken.None).ConfigureAwait(false);

            bool playbackResumed;
            lock (_gate)
            {
                playbackResumed = _sessions.Values.Any(active => PathsEqual(active.MediaPath, mediaPath));
            }

            if (playbackResumed)
            {
                await _nebulaManager.StartPlaybackPrefetchAsync(mediaPath, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-PLAYBACK-CACHE] Não foi possível cancelar o pré-cache inativo de {MediaPath}.", mediaPath);
        }
    }

    private static bool PathsEqual(string? left, string? right)
    {
        return string.Equals(
            left,
            right,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private sealed record ActivePlayback(string MediaPath, string? PlaySessionId);
}
