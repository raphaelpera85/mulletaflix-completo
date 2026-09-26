using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Plugins.ShortMax;

public sealed class ShortMaxClient : IDisposable
{
    public const string SiteRoot = "https://shorttv.live";
    private const string Locale = "pt";
    private static readonly TimeSpan RequestDelay = TimeSpan.FromMilliseconds(250);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ShortMaxClient> _logger;
    private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
    private DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;

    public ShortMaxClient(IHttpClientFactory httpClientFactory, ILogger<ShortMaxClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ShortMaxMatch>> SearchAsync(string? query, int maxResults, CancellationToken cancellationToken)
    {
        var results = new List<ShortMaxMatch>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        var directId = ShortMaxTitleMatcher.ExtractSeriesId(query);
        var searchTerm = directId ?? query;
        var html = await GetHtmlAsync($"/{Locale}/search/{Uri.EscapeDataString(searchTerm)}", cancellationToken).ConfigureAwait(false);
        if (html is null) return results;

        foreach (Match match in Regex.Matches(html, "<a[^>]+href=\"(?<href>/" + Locale + "/drama/[^\"]+)\"(?<body>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value);
            var seriesId = ShortMaxTitleMatcher.ExtractSeriesId(href);
            if (seriesId is null || results.Any(item => item.Series.SeriesId == seriesId)) continue;

            var body = match.Groups["body"].Value;
            var nameMatch = Regex.Match(body, "(?:alt|title)=\"(?<name>[^\"]+)\"", RegexOptions.IgnoreCase);
            var name = nameMatch.Success ? WebUtility.HtmlDecode(nameMatch.Groups["name"].Value) : NameFromPath(href, seriesId);
            var series = new ShortMaxSeries { SeriesId = seriesId, Name = ShortMaxTitleMatcher.CleanName(name), PagePath = href };
            results.Add(new ShortMaxMatch(series, ShortMaxTitleMatcher.Similarity(series.Name, query)));
        }

        if (directId is not null && results.Count == 0)
        {
            var direct = await GetSeriesAsync(directId, cancellationToken).ConfigureAwait(false);
            if (direct is not null) results.Add(new ShortMaxMatch(direct, 1));
        }

        return results.OrderByDescending(item => item.Score).Take(Math.Clamp(maxResults, 1, 20)).ToList();
    }

    public async Task<ShortMaxSeries?> GetSeriesAsync(string? seriesId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(seriesId)) return null;
        var search = await SearchAsync(seriesId, 1, cancellationToken).ConfigureAwait(false);
        var path = search.FirstOrDefault()?.Series.PagePath;
        if (string.IsNullOrWhiteSpace(path)) return null;
        var html = await GetHtmlAsync(path, cancellationToken).ConfigureAwait(false);
        if (html is null) return null;

        var title = ReadTag(html, "h1", "title") ?? search[0].Series.Name;
        var description = ReadMeta(html, "description");
        var cover = ReadMeta(html, "og:image");
        var series = new ShortMaxSeries
        {
            SeriesId = seriesId,
            Name = ShortMaxTitleMatcher.CleanName(WebUtility.HtmlDecode(title)),
            Overview = WebUtility.HtmlDecode(description ?? string.Empty),
            Cover = WebUtility.HtmlDecode(cover ?? string.Empty),
            PagePath = path
        };

        foreach (Match episode in Regex.Matches(html, "<a[^>]+href=\"(?<href>/" + Locale + "/episode/[^\"]+-(?<number>\\d+))\"[^>]*class=\"episode\"", RegexOptions.IgnoreCase))
        {
            if (int.TryParse(episode.Groups["number"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                series.Episodes = series.Episodes.Concat(new[] { new ShortMaxEpisode { Number = number, PagePath = episode.Groups["href"].Value } }).ToList();
            }
        }

        return series;
    }

    public void Dispose()
    {
        _requestLock.Dispose();
    }

    private async Task<string?> GetHtmlAsync(string path, CancellationToken cancellationToken)
    {
        await _requestLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var wait = RequestDelay - (DateTimeOffset.UtcNow - _lastRequestUtc);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            var response = await _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(SiteRoot + path, cancellationToken).ConfigureAwait(false);
            _lastRequestUtc = DateTimeOffset.UtcNow;
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "ShortMax request failed for {Path}", path);
            return null;
        }
        finally { _requestLock.Release(); }
    }

    private static string? ReadMeta(string html, string property)
    {
        var match = Regex.Match(html, "<meta[^>]+(?:property|name)=\"" + Regex.Escape(property) + "\"[^>]+content=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? ReadTag(string html, string tag, string attribute)
    {
        var match = Regex.Match(html, "<" + tag + "[^>]*>(?<value>.*?)</" + tag + ">", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? Regex.Replace(match.Groups["value"].Value, "<.*?>", string.Empty).Trim() : null;
    }

    private static string NameFromPath(string path, string id)
    {
        var value = path[(path.LastIndexOf('/') + 1)..];
        value = Regex.Replace(value, "-" + Regex.Escape(id) + "$", string.Empty, RegexOptions.IgnoreCase);
        return WebUtility.HtmlDecode(value.Replace('-', ' '));
    }
}
