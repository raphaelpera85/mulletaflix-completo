using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.MyDramaList
{
    public class MyDramaListSeriesImageProvider : IRemoteImageProvider, IHasOrder
    {
        /// <summary>
        /// How long the provider stays quiet after the site refuses a request.
        /// </summary>
        /// <remarks>
        /// Same failure mode already handled in <see cref="MyDramaListSeriesProvider"/>: the site answers
        /// 403 to this HTTP stack regardless of headers, so retrying per item cannot succeed. It only
        /// writes one ERROR line per item and burns a round trip. This provider is the latent half of
        /// that defect — it only runs for items that already carry a MyDramaList id, which is why it has
        /// not been noisy, but the behaviour would be one ERROR per item as soon as identification works.
        /// </remarks>
        public static readonly TimeSpan FailureCooldown = TimeSpan.FromMinutes(30);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MyDramaListSeriesImageProvider> _logger;

        private long _quietUntilTicks;

        public MyDramaListSeriesImageProvider(IHttpClientFactory httpClientFactory, ILogger<MyDramaListSeriesImageProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public string Name => "MyDramaList";

        /// <inheritdoc />
        public int Order => 2;

        /// <summary>
        /// Gets a value indicating whether the provider is currently quiet because the site refused
        /// a recent request.
        /// </summary>
        internal bool IsInFailureCooldown
        {
            get
            {
                var quietUntil = Interlocked.Read(ref _quietUntilTicks);
                return quietUntil != 0 && new DateTime(quietUntil, DateTimeKind.Utc) > DateTime.UtcNow;
            }
        }

        /// <inheritdoc />
        public bool Supports(BaseItem item)
        {
            return item is Series;
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[]
            {
                ImageType.Primary,
                ImageType.Backdrop
            };
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var results = new List<RemoteImageInfo>();
            var mdlId = item.GetProviderId("MyDramaList");

            if (string.IsNullOrEmpty(mdlId))
            {
                return results;
            }

            if (IsInFailureCooldown)
            {
                // The site is known to be refusing this HTTP stack; skip the request entirely instead
                // of paying a round trip (and an ERROR line) for every item.
                return results;
            }

            try
            {
                _logger.LogInformation("Fetching MyDramaList images for ID: {Id}", mdlId);
                var detailUrl = "https://mydramalist.com/" + mdlId;

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

                var html = await client.GetStringAsync(detailUrl, cancellationToken).ConfigureAwait(false);
                var jsonLdMatch = Regex.Match(html, @"<script type=""application/ld\+json"">\s*({""@context"":""https://schema\.org"",""@type"":""(?:TVSeries|Movie)"",.*?})\s*</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                if (jsonLdMatch.Success)
                {
                    var jsonNode = JsonNode.Parse(jsonLdMatch.Groups[1].Value);
                    var imgUrl = jsonNode?["image"]?.ToString();

                    if (!string.IsNullOrEmpty(imgUrl))
                    {
                        results.Add(new RemoteImageInfo
                        {
                            ProviderName = Name,
                            Type = ImageType.Primary,
                            Url = imgUrl
                        });

                        results.Add(new RemoteImageInfo
                        {
                            ProviderName = Name,
                            Type = ImageType.Backdrop,
                            Url = imgUrl
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                EnterFailureCooldown(ex, "images");
            }

            return results;
        }

        /// <summary>
        /// Arms the quiet period, reporting the reason once instead of once per item.
        /// </summary>
        /// <param name="exception">The failure.</param>
        /// <param name="operation">The operation that failed.</param>
        private void EnterFailureCooldown(Exception exception, string operation)
        {
            var quietUntil = DateTime.UtcNow.Add(FailureCooldown);
            var previous = Interlocked.Exchange(ref _quietUntilTicks, quietUntil.Ticks);

            if (previous != 0 && new DateTime(previous, DateTimeKind.Utc) > DateTime.UtcNow)
            {
                // Already quiet: this failure was expected and was reported by the call that armed the
                // cooldown. Logging it again is what produced one ERROR line per item.
                _logger.LogDebug(exception, "MyDramaList is still unavailable while resolving {Operation}.", operation);
                return;
            }

            _logger.LogWarning(
                exception,
                "MyDramaList refused a request while resolving {Operation}. Pausing image lookups for {Minutes} minutes instead of retrying for every item.",
                operation,
                FailureCooldown.TotalMinutes);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }
    }
}
