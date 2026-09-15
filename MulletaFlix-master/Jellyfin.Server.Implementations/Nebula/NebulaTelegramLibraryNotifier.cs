using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

    public NebulaTelegramLibraryNotifier(
        ILibraryManager libraryManager,
        INebulaFtpManager nebulaManager,
        ILogger<NebulaTelegramLibraryNotifier> logger)
    {
        _libraryManager = libraryManager;
        _nebulaManager = nebulaManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemAdded;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _cts.Cancel();
        return Task.CompletedTask;
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
                        sb.Append("⭐ <b>Nota:</b> ").Append(movie.CommunityRating.Value.ToString("0.0")).AppendLine(" / 10");
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
                    await _nebulaManager.SendTelegramNotificationAsync(message, null, _cts.Token).ConfigureAwait(false);
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
        }, _cts.Token);
    }

    public void Dispose()
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _cts.Cancel();
        _cts.Dispose();
    }
}
