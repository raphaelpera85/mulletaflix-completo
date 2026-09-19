using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Channels;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>Publica notificações de novas mídias através da camada extensível Notifications.</summary>
public sealed class NotificationsLibraryNotifier : IHostedService, IDisposable
{
    private sealed record WorkItem(string MessageHtml, string? ImagePath);

    private readonly ILibraryManager _libraryManager;
    private readonly INebulaFtpManager _nebulaManager;
    private readonly IServerApplicationHost _applicationHost;
    private readonly ILogger<NotificationsLibraryNotifier> _logger;
    private readonly ConcurrentDictionary<Guid, byte> _recentlyNotified = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _scheduled = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<WorkItem> _queue = Channel.CreateUnbounded<WorkItem>();
    private Task? _workerTask;

    public NotificationsLibraryNotifier(
        ILibraryManager libraryManager,
        INebulaFtpManager nebulaManager,
        IServerApplicationHost applicationHost,
        ILogger<NotificationsLibraryNotifier> logger)
    {
        _libraryManager = libraryManager;
        _nebulaManager = nebulaManager;
        _applicationHost = applicationHost;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemAdded;
        _libraryManager.ItemUpdated += OnItemUpdated;
        _workerTask = Task.Run(ProcessQueueAsync, CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemUpdated -= OnItemUpdated;
        _cts.Cancel();
        foreach (var scheduled in _scheduled.Values)
        {
            scheduled.Cancel();
        }
        if (_workerTask is not null)
        {
            await _workerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs args)
    {
        var item = args.Item;
        if (!IsNotifiableMedia(item) || _recentlyNotified.ContainsKey(item.Id))
        {
            return;
        }

        ScheduleMetadataCheck(item.Id);
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs args)
    {
        var item = args.Item;
        if (!IsNotifiableMedia(item) || _recentlyNotified.ContainsKey(item.Id) || !_scheduled.ContainsKey(item.Id))
        {
            return;
        }

        ScheduleMetadataCheck(item.Id);
    }

    private static bool IsNotifiableMedia(BaseItem? item)
        => item is not null && !item.IsFolder && !item.IsVirtualItem && (item is Movie || item is Episode);

    private void ScheduleMetadataCheck(Guid itemId)
    {
        var scheduled = new CancellationTokenSource();
        if (_scheduled.TryGetValue(itemId, out var previous))
        {
            previous.Cancel();
        }

        _scheduled[itemId] = scheduled;
        _ = Task.Run(() => WaitForMetadataAndQueueAsync(itemId, scheduled), CancellationToken.None);
    }

    private async Task WaitForMetadataAndQueueAsync(Guid itemId, CancellationTokenSource scheduled)
    {
        try
        {
            BaseItem? ready = null;
            string? previousSnapshot = null;
            for (var attempt = 0; attempt < 180 && !_cts.IsCancellationRequested; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt == 0 ? 2 : 1), scheduled.Token).ConfigureAwait(false);
                var current = _libraryManager.GetItemById(itemId);
                if (!IsNotifiableMedia(current))
                {
                    return;
                }

                var snapshot = GetMetadataSnapshot(current);
                if (snapshot == previousSnapshot && IsMetadataReady(current))
                {
                    ready = current;
                    break;
                }

                previousSnapshot = snapshot;
            }

            if (ready is null)
            {
                _logger.LogWarning("[NOTIFICATIONS] Capa/NFO da mídia {ItemId} não estabilizaram em 180 segundos; nenhuma notificação foi enviada.", itemId);
                return;
            }

            var settings = _nebulaManager.GetNotificationsSettings();
            if (!settings.Enabled || settings.ChannelIds.Count == 0)
            {
                return;
            }

            if (!_recentlyNotified.TryAdd(itemId, 0))
            {
                return;
            }

            var message = BuildMessage(ready, settings.PublicServerUrl, _applicationHost.SystemId);
            _queue.Writer.TryWrite(new WorkItem(message, GetCoverPath(ready)));
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested || scheduled.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NOTIFICATIONS] Falha ao preparar a notificação da mídia {ItemId}.", itemId);
        }
        finally
        {
            if (_scheduled.TryGetValue(itemId, out var current) && ReferenceEquals(current, scheduled))
            {
                _scheduled.TryRemove(itemId, out _);
            }

            scheduled.Dispose();
        }
    }

    private static string GetMetadataSnapshot(BaseItem item)
    {
        var image = item.GetImagePath(ImageType.Primary);
        var nfo = GetNfoPath(item);
        var nfoState = !string.IsNullOrWhiteSpace(nfo) && File.Exists(nfo)
            ? $"{new FileInfo(nfo).Length}:{File.GetLastWriteTimeUtc(nfo).Ticks}"
            : "missing";
        var imageState = !string.IsNullOrWhiteSpace(image) && File.Exists(image)
            ? $"{new FileInfo(image).Length}:{File.GetLastWriteTimeUtc(image).Ticks}"
            : "missing";
        return string.Join('|', item.Name, item.Overview, item.ProductionYear, image, imageState, nfo, nfoState);
    }

    private bool IsMetadataReady(BaseItem item)
    {
        var imagePath = FindCoverPath(item);
        var nfoPath = GetNfoPath(item);
        return !string.IsNullOrWhiteSpace(item.Name)
            && !string.IsNullOrWhiteSpace(imagePath)
            && File.Exists(imagePath)
            && !string.IsNullOrWhiteSpace(nfoPath)
            && File.Exists(nfoPath)
            && new FileInfo(nfoPath).Length > 0;
    }

    private static string? GetNfoPath(BaseItem item)
        => string.IsNullOrWhiteSpace(item.Path) ? null : Path.ChangeExtension(item.Path, ".nfo");

    private string? GetCoverPath(BaseItem item)
    {
        var path = FindCoverPath(item);
        if (!string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        _logger.LogWarning("[NOTIFICATIONS] A mídia {ItemId} foi estabilizada sem capa disponível; será enviado texto.", item.Id);
        return null;
    }

    private static string? FindCoverPath(BaseItem item)
    {
        var path = item.GetImagePath(ImageType.Primary);
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            return path;
        }

        if (item is Episode episode && episode.Series is not null)
        {
            path = episode.Series.GetImagePath(ImageType.Primary);
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    internal static string BuildMessage(BaseItem item, string publicServerUrl, string serverId)
    {
        var link = BuildDirectWebUrl(item.Id, publicServerUrl, serverId);
        var sb = new StringBuilder();
        if (item is Episode episode)
        {
            var seriesName = episode.SeriesName ?? episode.FindSeriesName() ?? "Série";
            sb.Append("📺 <b>").Append(WebUtility.HtmlEncode(seriesName)).AppendLine("</b>");
            sb.Append("📚 Temporada ").Append(episode.ParentIndexNumber ?? 1).Append(" • Episódio ").Append(episode.IndexNumber ?? 1).AppendLine();
            if (!string.IsNullOrWhiteSpace(episode.Name))
            {
                sb.Append("🎬 <b>").Append(WebUtility.HtmlEncode(episode.Name)).AppendLine("</b>");
            }
        }
        else
        {
            sb.Append("🎬 <b>").Append(WebUtility.HtmlEncode(item.Name ?? "Nova mídia")).Append("</b>");
            if (item.ProductionYear.HasValue)
            {
                sb.Append(" (").Append(item.ProductionYear.Value).Append(')');
            }

            sb.AppendLine();
        }

        sb.Append("🔗 <a href=\"").Append(WebUtility.HtmlEncode(link)).AppendLine("\">Abrir no MulletaFlix</a>");
        return sb.ToString().TrimEnd();
    }

    internal static string BuildDirectWebUrl(Guid itemId, string publicServerUrl, string serverId)
        => $"{publicServerUrl.TrimEnd('/')}/web/#/details?id={itemId:N}&serverId={serverId}";

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                while (_queue.Reader.TryRead(out var work))
                {
                    var settings = _nebulaManager.GetNotificationsSettings();
                    if (!settings.Enabled || settings.ChannelIds.Count == 0)
                    {
                        continue;
                    }

                    foreach (var channelId in settings.ChannelIds)
                    {
                        if (!await _nebulaManager.SendNotificationAsync(work.MessageHtml, work.ImagePath, channelId, _cts.Token).ConfigureAwait(false))
                        {
                            _logger.LogWarning("[NOTIFICATIONS] Falha ao enviar notificação para o canal {ChannelId}.", channelId);
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(settings.IntervalSeconds), _cts.Token).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NOTIFICATIONS] Worker da fila encerrou com erro.");
        }
    }

    public void Dispose()
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemUpdated -= OnItemUpdated;
        _cts.Cancel();
        _queue.Writer.TryComplete();
        _cts.Dispose();
    }
}
