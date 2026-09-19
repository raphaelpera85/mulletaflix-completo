using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Nebula;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Serviço de notificação nativa para Telegram quando novos itens de mídia são adicionados à biblioteca.
/// </summary>
public sealed class NebulaTelegramLibraryNotifier : IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly INebulaFtpManager _nebulaManager;
    private readonly ILogger<NebulaTelegramLibraryNotifier> _logger;
    private readonly ConcurrentDictionary<Guid, byte> _recentlyNotified = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Channel<string> _notificationQueue = Channel.CreateUnbounded<string>();
    private Task? _workerTask;

    public NebulaTelegramLibraryNotifier(
        ILibraryManager libraryManager,
        INebulaFtpManager nebulaManager,
        ILogger<NebulaTelegramLibraryNotifier> logger)
    {
        _libraryManager = libraryManager;
        _nebulaManager = nebulaManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemAdded;
        _workerTask = Task.Run(ProcessQueueAsync, CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _cts.Cancel();
        if (_workerTask is not null)
        {
            await _workerTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs args)
    {
        var item = args.Item;
        if (item is null || item.IsFolder || item.IsVirtualItem)
        {
            return;
        }

        if (item is not Movie && item is not Episode)
        {
            return;
        }

        if (!_recentlyNotified.TryAdd(item.Id, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2000, _cts.Token).ConfigureAwait(false);

                var sb = new StringBuilder();
                if (item is Movie movie)
                {
                    sb.AppendLine("🍿 <b>Novo Filme no MulletaFlix!</b>");
                    sb.AppendLine();
                    sb.Append("🎬 <b>").Append(System.Net.WebUtility.HtmlEncode(movie.Name)).Append("</b>");
                    if (movie.ProductionYear.HasValue)
                    {
                        sb.Append(" (").Append(movie.ProductionYear.Value).Append(')');
                    }

                    sb.AppendLine();

                    if (movie.CommunityRating.HasValue)
                    {
                        sb.Append("⭐ <b>Nota:</b> ").Append(movie.CommunityRating.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(" / 10");
                    }

                    if (movie.Genres is { Length: > 0 })
                    {
                        sb.Append("🎭 <b>Gênero:</b> ").AppendLine(string.Join(", ", movie.Genres.Take(3)));
                    }

                    if (!string.IsNullOrWhiteSpace(movie.Overview))
                    {
                        var overview = movie.Overview.Length > 250
                            ? string.Concat(movie.Overview.AsSpan(0, 247), "...")
                            : movie.Overview;
                        sb.AppendLine().Append("📝 ").AppendLine(System.Net.WebUtility.HtmlEncode(overview));
                    }
                }
                else if (item is Episode episode)
                {
                    var seriesName = episode.SeriesName ?? episode.FindSeriesName() ?? "Série";
                    sb.AppendLine("📺 <b>Novo Episódio no MulletaFlix!</b>");
                    sb.AppendLine();
                    sb.Append("🎬 <b>").Append(System.Net.WebUtility.HtmlEncode(seriesName)).Append("</b>");
                    sb.AppendLine();
                    sb.Append("📌 <b>T").Append(episode.ParentIndexNumber ?? 1).Append(":E").Append(episode.IndexNumber ?? 1).Append("</b> - ").AppendLine(System.Net.WebUtility.HtmlEncode(episode.Name ?? string.Empty));

                    if (!string.IsNullOrWhiteSpace(episode.Overview))
                    {
                        var overview = episode.Overview.Length > 200
                            ? string.Concat(episode.Overview.AsSpan(0, 197), "...")
                            : episode.Overview;
                        sb.AppendLine().Append("📝 ").AppendLine(System.Net.WebUtility.HtmlEncode(overview));
                    }
                }

                var message = sb.ToString().TrimEnd();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    _notificationQueue.Writer.TryWrite(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Servidor finalizando
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEBULA-NOTIFIER] Falha ao enviar notificação de item adicionado para o Telegram.");
            }
        });
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (await _notificationQueue.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
            {
                while (_notificationQueue.Reader.TryRead(out var message))
                {
                    var settings = _nebulaManager.GetTelegramNotificationSettings();
                    if (!settings.Enabled)
                    {
                        _notificationQueue.Writer.TryWrite(message);
                        await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token).ConfigureAwait(false);
                        break;
                    }

                    foreach (var chatId in settings.ChatIds)
                    {
                        var delivered = await _nebulaManager.SendTelegramNotificationAsync(message, chatId, _cts.Token).ConfigureAwait(false);
                        if (!delivered)
                        {
                            _logger.LogWarning("[NEBULA-NOTIFIER] Falha ao enviar notificação para o chat {ChatId}.", chatId);
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
            _logger.LogError(ex, "[NEBULA-NOTIFIER] Worker da fila de notificações encerrou com erro.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _cts.Cancel();
        _notificationQueue.Writer.TryComplete();
        _cts.Dispose();
    }
}
