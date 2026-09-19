using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Branding;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.MediaEncoding;

public sealed class StrmPrebufferManager : IStrmPrebufferManager, IDisposable
{
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36";
    private static readonly TimeSpan ReadIdleTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(30);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly Timer _cleanupTimer;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerConfigurationManager _configurationManager;
    private readonly IServerApplicationHost _applicationHost;
    private readonly ILogger<StrmPrebufferManager> _logger;

    public StrmPrebufferManager(
        IHttpClientFactory httpClientFactory,
        IServerConfigurationManager configurationManager,
        IServerApplicationHost applicationHost,
        ILogger<StrmPrebufferManager> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configurationManager = configurationManager;
        _applicationHost = applicationHost;
        _logger = logger;
        _cleanupTimer = new Timer(_ => CleanupExpiredSessions(), null, SessionTtl, SessionTtl);
    }

    public async Task PrepareAsync(BaseItem item)
    {
        CleanupExpiredSessions();
        var options = _configurationManager.GetConfiguration<BrandingOptions>("branding");
        var prebufferActive = options.PrebufferEnabled || options.IntroEnabled || !string.IsNullOrWhiteSpace(options.IntroPath);
        if (!prebufferActive || item.Path is null)
        {
            return;
        }

        if (item.Path.EndsWith(".strm", StringComparison.OrdinalIgnoreCase))
        {
            if (_sessions.ContainsKey(item.Id))
            {
                return;
            }

            string url;
            try
            {
                url = (await File.ReadAllTextAsync(item.Path).ConfigureAwait(false)).Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to read STRM item {ItemPath} for prebuffering", item.Path);
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var upstreamUri)
                || (upstreamUri.Scheme != Uri.UriSchemeHttp && upstreamUri.Scheme != Uri.UriSchemeHttps))
            {
                return;
            }

            var session = new Session(upstreamUri, Math.Clamp(options.PrebufferSizeMb, 1, 256) * 1024L * 1024L);
            if (!_sessions.TryAdd(item.Id, session))
            {
                session.Dispose();
                return;
            }

            _ = FillAsync(item.Id, session);
            return;
        }

        if (File.Exists(item.Path))
        {
            try
            {
                await using var fs = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
                var headerBuf = new byte[64 * 1024];
                _ = await fs.ReadAsync(headerBuf, 0, headerBuf.Length).ConfigureAwait(false);
                _logger.LogDebug("Filesystem cache pre-warmed for {ItemPath}", item.Path);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Warmup read failed for local item {ItemPath}", item.Path);
            }
        }
    }

    public bool TryGetProxyUrl(Guid itemId, out string url)
    {
        return TryGetProxyUrl(itemId, null, out url);
    }

    public bool TryGetProxyUrl(Guid itemId, string? apiKey, out string url)
    {
        if (_sessions.ContainsKey(itemId))
        {
            if (_sessions.TryGetValue(itemId, out var session))
            {
                session.Touch();
            }

            url = $"{_applicationHost.GetSmartApiUrl("localhost")}/Videos/{itemId:N}/Prebuffer";
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                url += $"?ApiKey={Uri.EscapeDataString(apiKey)}";
            }

            return true;
        }

        url = string.Empty;
        return false;
    }

    public string? GetContentType(Guid itemId)
    {
        return _sessions.TryGetValue(itemId, out var session) ? session.ContentType : null;
    }

    public async Task<(string ContentType, long? ContentLength)> CopyToAsync(Guid itemId, Stream output, CancellationToken cancellationToken)
    {
        CleanupExpiredSessions();
        if (!_sessions.TryGetValue(itemId, out var session))
        {
            throw new FileNotFoundException("STRM prebuffer session not found.");
        }

        try
        {
            session.Touch();
            await session.Ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
            await using (var prefix = new FileStream(session.BufferPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                await prefix.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            using var request = CreateUpstreamRequest(session.Uri);
            request.Headers.Range = new RangeHeaderValue(session.BufferedBytes, null);
            using var response = await _httpClientFactory.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                return (session.ContentType ?? "application/octet-stream", session.BufferedBytes);
            }

            response.EnsureSuccessStatusCode();
            await using var upstream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.PartialContent)
            {
                await SkipAsync(upstream, session.BufferedBytes, cancellationToken).ConfigureAwait(false);
            }

            await upstream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            return (session.ContentType ?? response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream", response.Content.Headers.ContentLength);
        }
        finally
        {
            // ponytail: keep completed sessions alive to avoid clobbering concurrent readers on the same item; add TTL cleanup if temp usage grows.
        }
    }

    private async Task FillAsync(Guid itemId, Session session)
    {
        var completed = false;
        try
        {
            using var request = CreateUpstreamRequest(session.Uri);
            using var response = await _httpClientFactory.CreateClient().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _disposeCts.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            session.ContentType = response.Content.Headers.ContentType?.ToString();
            await using var input = await response.Content.ReadAsStreamAsync(_disposeCts.Token).ConfigureAwait(false);
            await using var output = new FileStream(session.BufferPath, FileMode.Create, FileAccess.Write, FileShare.Read, 81920, true);
            var buffer = new byte[81920];
            while (session.BufferedBytes < session.MaxBytes)
            {
                var read = await ReadWithIdleTimeoutAsync(input, buffer, _disposeCts.Token).ConfigureAwait(false);
                if (read == 0) break;
                var count = (int)Math.Min(read, session.MaxBytes - session.BufferedBytes);
                await output.WriteAsync(buffer.AsMemory(0, count), _disposeCts.Token).ConfigureAwait(false);
                session.BufferedBytes += count;
                if (count != read) break;
            }
            completed = true;
        }
        catch (OperationCanceledException) when (_disposeCts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to prebuffer STRM item {ItemId}", itemId);
        }
        finally
        {
            session.Ready.TrySetResult(true);
            if (!completed && _sessions.TryRemove(itemId, out var removedSession))
            {
                removedSession.Dispose();
            }
        }
    }

    private static async Task<int> ReadWithIdleTimeoutAsync(Stream input, byte[] buffer, CancellationToken cancellationToken)
    {
        using var idleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idleCancellation.CancelAfter(ReadIdleTimeout);
        return await input.ReadAsync(buffer.AsMemory(), idleCancellation.Token).ConfigureAwait(false);
    }

    private void CleanupExpiredSessions()
    {
        var cutoff = DateTime.UtcNow - SessionTtl;
        foreach (var pair in _sessions)
        {
            if (pair.Value.LastAccessUtc < cutoff && _sessions.TryRemove(pair))
            {
                pair.Value.Dispose();
            }
        }
    }

    private static async Task SkipAsync(Stream stream, long bytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        while (bytes > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, bytes)), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            bytes -= read;
        }
    }

    private static HttpRequestMessage CreateUpstreamRequest(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);

        var origin = uri.GetLeftPart(UriPartial.Authority);
        request.Headers.Referrer = new Uri(origin + "/");
        request.Headers.TryAddWithoutValidation("Origin", origin);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");

        return request;
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        _cleanupTimer.Dispose();
        foreach (var session in _sessions.Values) session.Dispose();
        _sessions.Clear();
        _disposeCts.Dispose();
    }

    private sealed class Session : IDisposable
    {
        public Session(Uri uri, long maxBytes)
        {
            Uri = uri;
            MaxBytes = maxBytes;
            BufferPath = Path.Combine(Path.GetTempPath(), "MulletaFlix", "prebuffer", Guid.NewGuid().ToString("N") + ".bin");
            Directory.CreateDirectory(Path.GetDirectoryName(BufferPath)!);
        }

        public Uri Uri { get; }
        public string BufferPath { get; }
        public long MaxBytes { get; }
        public long BufferedBytes { get; set; }
        public string? ContentType { get; set; }
        public TaskCompletionSource<bool> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DateTime LastAccessUtc { get; private set; } = DateTime.UtcNow;

        public void Touch()
        {
            LastAccessUtc = DateTime.UtcNow;
        }

        public void Dispose()
        {
            try { File.Delete(BufferPath); } catch { }
        }
    }
}
