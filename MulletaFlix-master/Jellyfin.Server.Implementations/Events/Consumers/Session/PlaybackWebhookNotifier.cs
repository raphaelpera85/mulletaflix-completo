using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Server.Implementations.Events.Consumers.Session
{
    /// <summary>
    /// Posts playback events to configured webhooks (e.g. Discord, Telegram).
    /// Configuration is read from environment variables:
    /// <list type="bullet">
    /// <item><c>MulletaFlix_WEBHOOK_URL</c> — comma-separated list of webhook URLs.</item>
    /// <item><c>MulletaFlix_WEBHOOK_EVENTS</c> — comma-separated list of event names (PlaybackStart, PlaybackStop). Defaults to all.</item>
    /// </list>
    /// </summary>
    public class PlaybackWebhookNotifier :
        IEventConsumer<PlaybackStartEventArgs>,
        IEventConsumer<PlaybackStopEventArgs>
    {
        private const string WebhookUrlEnv = "MulletaFlix_WEBHOOK_URL";
        private const string WebhookEventsEnv = "MulletaFlix_WEBHOOK_EVENTS";

        private readonly ILogger<PlaybackWebhookNotifier> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IReadOnlyList<string> _webhookUrls;
        private readonly HashSet<string> _enabledEvents;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaybackWebhookNotifier"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public PlaybackWebhookNotifier(ILogger<PlaybackWebhookNotifier> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;

            _webhookUrls = ParseUrls(Environment.GetEnvironmentVariable(WebhookUrlEnv));
            _enabledEvents = ParseEvents(Environment.GetEnvironmentVariable(WebhookEventsEnv));
        }

        /// <inheritdoc />
        public Task OnEvent(PlaybackStartEventArgs eventArgs)
            => SendIfEnabledAsync("PlaybackStart", eventArgs);

        /// <inheritdoc />
        public Task OnEvent(PlaybackStopEventArgs eventArgs)
            => SendIfEnabledAsync("PlaybackStop", eventArgs);

        private Task SendIfEnabledAsync(string eventName, PlaybackProgressEventArgs eventArgs)
        {
            if (_webhookUrls.Count == 0)
            {
                return Task.CompletedTask;
            }

            if (_enabledEvents.Count > 0 && !_enabledEvents.Contains(eventName))
            {
                return Task.CompletedTask;
            }

            if (eventArgs.MediaInfo is null || eventArgs.Users.Count == 0)
            {
                return Task.CompletedTask;
            }

            return SendAsync(eventName, eventArgs, CancellationToken.None);
        }

        private async Task SendAsync(string eventName, PlaybackProgressEventArgs eventArgs, CancellationToken cancellationToken)
        {
            var user = eventArgs.Users[0];
            var item = eventArgs.MediaInfo;

            var payload = new Dictionary<string, object?>
            {
                ["event"] = eventName,
                ["user"] = user.Username,
                ["item"] = item.Name,
                ["itemType"] = item.Type,
                ["device"] = eventArgs.DeviceName,
                ["client"] = eventArgs.ClientName,
                ["seriesName"] = item.SeriesName,
                ["overview"] = item.Overview,
                ["timestampUtc"] = DateTime.UtcNow.ToString("O")
            };

            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            foreach (var url in _webhookUrls)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    var response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "Webhook {EventName} to {Url} returned {StatusCode}.",
                            eventName,
                            url,
                            (int)response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Webhook {EventName} to {Url} failed.", eventName, url);
                }
            }
        }

        private static IReadOnlyList<string> ParseUrls(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<string>();
            }

            var urls = new List<string>();
            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Uri.TryCreate(part, UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    urls.Add(part);
                }
            }

            return urls;
        }

        private static HashSet<string> ParseEvents(string? raw)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                result.Add(part);
            }

            return result;
        }
    }
}
