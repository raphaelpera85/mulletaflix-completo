using System;
using System.Collections.Generic;
using System.Diagnostics;
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
public sealed class NebulaPlaybackSessionMonitor : IHostedService, IDisposable
{
    /// <summary>
    /// Nome da fonte OpenTelemetry das sessões de reprodução Nebula.
    /// </summary>
    public const string ActivitySourceName = "MulletaFlix.Nebula.PlaybackSession";

    private static readonly ActivitySource PlaybackActivitySource = new(ActivitySourceName);

    private readonly ISessionManager _sessionManager;
    private readonly INebulaFtpManager _nebulaManager;
    private readonly ILogger<NebulaPlaybackSessionMonitor> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, ActivePlayback> _sessions = new(StringComparer.Ordinal);
    private readonly HashSet<Task> _eventTasks = [];
    private CancellationTokenSource? _eventCancellation;
    private bool _isStarted;

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
        lock (_gate)
        {
            if (_isStarted)
            {
                return Task.CompletedTask;
            }

            _isStarted = true;
            _eventCancellation = new CancellationTokenSource();
            _sessionManager.PlaybackStart += OnPlaybackStart;
            _sessionManager.PlaybackStopped += OnPlaybackStopped;
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        CancellationTokenSource? eventCancellation;
        Task[] eventTasks;
        lock (_gate)
        {
            _isStarted = false;
            _sessionManager.PlaybackStart -= OnPlaybackStart;
            _sessionManager.PlaybackStopped -= OnPlaybackStopped;
            _sessions.Clear();
            eventCancellation = _eventCancellation;
            _eventCancellation = null;
            eventTasks = _eventTasks.ToArray();
        }

        if (eventCancellation is null)
        {
            return;
        }

        var drain = Task.WhenAll(eventTasks);
        try
        {
            await eventCancellation.CancelAsync().ConfigureAwait(false);
            await drain.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            DisposeCancellationSourceAfterDrain(eventCancellation, drain);
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? eventCancellation;
        Task[] eventTasks;
        lock (_gate)
        {
            _isStarted = false;
            _sessionManager.PlaybackStart -= OnPlaybackStart;
            _sessionManager.PlaybackStopped -= OnPlaybackStopped;
            _sessions.Clear();
            eventCancellation = _eventCancellation;
            _eventCancellation = null;
            eventTasks = _eventTasks.ToArray();
        }

        if (eventCancellation is not null)
        {
            eventCancellation.Cancel();
            DisposeCancellationSourceAfterDrain(eventCancellation, Task.WhenAll(eventTasks));
        }
    }

    private static void DisposeCancellationSourceAfterDrain(CancellationTokenSource source, Task drain)
    {
        if (drain.IsCompleted)
        {
            source.Dispose();
            return;
        }

        _ = drain.ContinueWith(
            _ => source.Dispose(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal async Task TrackPlaybackStartAsync(string? sessionId, string? playSessionId, string? mediaPath)
        => await TrackPlaybackStartCoreAsync(sessionId, playSessionId, mediaPath, requireStarted: false, CancellationToken.None).ConfigureAwait(false);

    private async Task TrackPlaybackStartCoreAsync(
        string? sessionId,
        string? playSessionId,
        string? mediaPath,
        bool requireStarted,
        CancellationToken cancellationToken)
    {
        using var activity = PlaybackActivitySource.StartActivity("nebula.playback.session_start", ActivityKind.Internal);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            activity?.SetTag("nebula.playback.result", "ignored");
            return;
        }

        string? pathToCancel = null;
        int activeSessions;
        lock (_gate)
        {
            if (requireStarted && !_isStarted)
            {
                activity?.SetTag("nebula.playback.result", "ignored");
                activity?.SetTag("nebula.playback.active_sessions", _sessions.Count);
                return;
            }

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

            activeSessions = _sessions.Count;
        }

        // Somente números e booleanos entram no span: o caminho da mídia e os
        // identificadores de sessão são dados do usuário.
        activity?.SetTag("nebula.playback.result", "tracked");
        activity?.SetTag("nebula.playback.prefetch_cancelled", pathToCancel is not null);
        activity?.SetTag("nebula.playback.active_sessions", activeSessions);

        await CancelIfUnusedAsync(pathToCancel, cancellationToken, requireStarted).ConfigureAwait(false);
    }

    internal async Task TrackPlaybackStoppedAsync(string? sessionId, string? playSessionId, string? mediaPath = null)
        => await TrackPlaybackStoppedCoreAsync(sessionId, playSessionId, mediaPath, requireStarted: false, CancellationToken.None).ConfigureAwait(false);

    private async Task TrackPlaybackStoppedCoreAsync(
        string? sessionId,
        string? playSessionId,
        string? mediaPath,
        bool requireStarted,
        CancellationToken cancellationToken)
    {
        using var activity = PlaybackActivitySource.StartActivity("nebula.playback.session_stop", ActivityKind.Internal);
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            activity?.SetTag("nebula.playback.result", "ignored");
            return;
        }

        string? pathToCancel = null;
        int activeSessions;
        lock (_gate)
        {
            if (requireStarted && !_isStarted)
            {
                activity?.SetTag("nebula.playback.result", "ignored");
                activity?.SetTag("nebula.playback.active_sessions", _sessions.Count);
                return;
            }

            if (!_sessions.TryGetValue(sessionId, out var active))
            {
                activity?.SetTag("nebula.playback.result", "ignored");
                activity?.SetTag("nebula.playback.active_sessions", _sessions.Count);
                return;
            }

            var hasPlaySessionIdentity = !string.IsNullOrWhiteSpace(active.PlaySessionId)
                || !string.IsNullOrWhiteSpace(playSessionId);
            if (hasPlaySessionIdentity
                    ? !string.Equals(active.PlaySessionId, playSessionId, StringComparison.Ordinal)
                    : string.IsNullOrWhiteSpace(mediaPath)
                        || !PathsEqual(active.MediaPath, mediaPath))
            {
                activity?.SetTag("nebula.playback.result", "identity_mismatch");
                activity?.SetTag("nebula.playback.active_sessions", _sessions.Count);
                return;
            }

            _sessions.Remove(sessionId);
            if (!_sessions.Values.Any(other => PathsEqual(other.MediaPath, active.MediaPath)))
            {
                pathToCancel = active.MediaPath;
            }

            activeSessions = _sessions.Count;
        }

        activity?.SetTag("nebula.playback.result", "tracked");
        activity?.SetTag("nebula.playback.prefetch_cancelled", pathToCancel is not null);
        activity?.SetTag("nebula.playback.active_sessions", activeSessions);

        await CancelIfUnusedAsync(pathToCancel, cancellationToken, requireStarted).ConfigureAwait(false);
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs args)
    {
        QueuePlaybackEvent(cancellationToken => TrackPlaybackStartCoreAsync(
            args.Session?.Id,
            args.PlaySessionId,
            args.Item?.Path,
            requireStarted: true,
            cancellationToken));
    }

    private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs args)
    {
        QueuePlaybackEvent(cancellationToken => TrackPlaybackStoppedCoreAsync(
            args.Session?.Id,
            args.PlaySessionId,
            args.Item?.Path,
            requireStarted: true,
            cancellationToken));
    }

    private void QueuePlaybackEvent(Func<CancellationToken, Task> handleEvent)
    {
        lock (_gate)
        {
            if (!_isStarted || _eventCancellation is null)
            {
                return;
            }

            var cancellationToken = _eventCancellation.Token;
            var task = ObservePlaybackEventAsync(() => handleEvent(cancellationToken), cancellationToken);
            _eventTasks.Add(task);
            _ = task.ContinueWith(
                completedTask =>
                {
                    lock (_gate)
                    {
                        _eventTasks.Remove(completedTask);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private async Task ObservePlaybackEventAsync(Func<Task> handleEvent, CancellationToken cancellationToken)
    {
        try
        {
            await handleEvent().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-PLAYBACK-CACHE] Erro ao processar evento de sessão de reprodução.");
        }
    }

    private async Task CancelIfUnusedAsync(string? mediaPath, CancellationToken cancellationToken, bool requireStarted)
    {
        if (string.IsNullOrWhiteSpace(mediaPath))
        {
            return;
        }

        try
        {
            await _nebulaManager.CancelPlaybackPrefetchAsync(mediaPath, cancellationToken).ConfigureAwait(false);

            bool playbackResumed;
            lock (_gate)
            {
                playbackResumed = (!requireStarted || _isStarted)
                    && _sessions.Values.Any(active => PathsEqual(active.MediaPath, mediaPath));
            }

            if (playbackResumed)
            {
                await _nebulaManager.StartPlaybackPrefetchAsync(mediaPath, cancellationToken).ConfigureAwait(false);
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
