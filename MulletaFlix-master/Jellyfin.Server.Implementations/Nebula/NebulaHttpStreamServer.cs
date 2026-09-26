#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Prometheus;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Servidor HTTP nativo em C# (porta 2123 por padrão para MulletaFlix) para streaming direto de mídias armazenadas no Telegram via Nebula.
/// Fornece suporte completo a HTTP 206 (Partial Content), cabeçalhos Range para busca instantânea (seeking) e fallback de reprodução.
/// </summary>
public sealed class NebulaHttpStreamServer : IAsyncDisposable, IDisposable
{
    private static readonly TimeSpan StreamRequestTimeout = TimeSpan.FromHours(2);
    private static readonly TimeSpan ControlRequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The copy buffer size used while streaming a response body.</summary>
    private const int StreamBufferSize = 128 * 1024;
    private static readonly Counter RequestCounter = Metrics.CreateCounter(
        "nebula_http_requests_total",
        "Total de requisições processadas pelo servidor HTTP Nebula.",
        new CounterConfiguration { LabelNames = new[] { "route", "status" } });
    private static readonly Counter SaturatedRequestCounter = Metrics.CreateCounter(
        "nebula_http_saturated_requests_total",
        "Total de requisições rejeitadas por limite de concorrência no servidor HTTP Nebula.",
        new CounterConfiguration { LabelNames = new[] { "route" } });
    private static readonly Histogram RequestDuration = Metrics.CreateHistogram(
        "nebula_http_request_duration_seconds",
        "Duração das requisições HTTP do servidor Nebula em segundos.",
        new HistogramConfiguration { LabelNames = new[] { "route" } });

    private readonly NebulaMongoContext _mongoContext;
    private readonly NebulaTelegramPool _telegramPool;
    private readonly string _host;
    private readonly int _port;
    private readonly string _streamToken;
    private NebulaPlaybackCache? _playbackCache;
    private readonly ILogger<NebulaHttpStreamServer> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lifecycleLock = new();
    private readonly SemaphoreSlim _streamConcurrency;

    private HttpListener? _listener;
    private Task? _listenTask;
    private bool _disposed;

    public bool IsRunning => _listener?.IsListening == true;

    /// <summary>Atualiza a referência ao cache de reprodução.</summary>
    public void SetPlaybackCache(NebulaPlaybackCache? cache)
    {
        _playbackCache = cache;
    }

    /// <summary>Inicia, sem bloquear a reprodução, o pré-cache de todos os blocos da mídia.</summary>
    public async Task<bool> StartPlaybackPrefetchAsync(string mediaPath, CancellationToken cancellationToken = default)
    {
        if (_playbackCache is null || string.IsNullOrWhiteSpace(mediaPath))
        {
            return false;
        }

        try
        {
            var identifier = await ResolveMediaIdentifierAsync(mediaPath, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return false;
            }

            var doc = await _mongoContext.FindFileByVirtualPathOrNameAsync(identifier, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return false;
            }

            var (parts, totalSize) = BuildStreamParts(doc);
            if (totalSize <= 0 || parts.Count == 0 || parts.Any(part => string.IsNullOrWhiteSpace(part.FileId) || part.ChatId == 0 || part.MessageId == 0))
            {
                return false;
            }

            await using var stream = new NebulaChunkedStream(_telegramPool, parts, totalSize, _logger, _playbackCache, GetMediaCacheKey(doc));
            _logger.LogInformation("[NEBULA-PLAYBACK-CACHE] Pré-cache iniciado no começo da intro para {MediaName} ({Size} bytes).", doc.GetValue("name", "media.bin").AsString, totalSize);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-PLAYBACK-CACHE] Não foi possível antecipar o cache para {MediaPath}; a reprodução seguirá normalmente.", mediaPath);
            return false;
        }
    }

    private static async Task<string?> ResolveMediaIdentifierAsync(string mediaPath, CancellationToken cancellationToken)
    {
        var candidate = mediaPath;
        if (File.Exists(mediaPath) && string.Equals(Path.GetExtension(mediaPath), ".strm", StringComparison.OrdinalIgnoreCase))
        {
            candidate = (await File.ReadAllTextAsync(mediaPath, cancellationToken).ConfigureAwait(false)).Trim();
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var parameter in query)
            {
                var separator = parameter.IndexOf('=', StringComparison.Ordinal);
                var key = separator < 0 ? parameter : parameter[..separator];
                if (!string.Equals(Uri.UnescapeDataString(key), "id", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var value = separator < 0 ? string.Empty : parameter[(separator + 1)..];
                return Uri.UnescapeDataString(value.Replace('+', ' '));
            }

            return null;
        }

        return candidate;
    }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaHttpStreamServer"/>.
    /// </summary>
    public NebulaHttpStreamServer(
        NebulaMongoContext mongoContext,
        NebulaTelegramPool telegramPool,
        string host,
        int port,
        ILogger<NebulaHttpStreamServer> logger,
        string streamToken = "",
        int maxActiveConnections = 32,
        NebulaPlaybackCache? playbackCache = null)
    {
        _mongoContext = mongoContext ?? throw new ArgumentNullException(nameof(mongoContext));
        _telegramPool = telegramPool ?? throw new ArgumentNullException(nameof(telegramPool));
        _host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
        _port = port > 0 ? port : 2123;
        _logger = logger;
        _playbackCache = playbackCache;
        _streamToken = streamToken ?? string.Empty;
        _streamConcurrency = new SemaphoreSlim(Math.Clamp(maxActiveConnections, 1, 4096));
    }

    /// <summary>
    /// Inicia o listener HTTP na porta configurada.
    /// </summary>
    public void Start()
    {
        lock (_lifecycleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_listener != null && _listener.IsListening)
            {
                return;
            }

            HttpListener? listener = null;
            try
            {
                listener = new HttpListener();
                // 0.0.0.0 is a bind address, not a valid Host header for HttpListener.
                // Use the HTTP.sys wildcard so generated STRM URLs can be consumed from LAN clients.
                var prefixHost = _host is "0.0.0.0" or "*" or "+" ? "+" : _host;
                var prefix = $"http://{prefixHost}:{_port}/";
                listener.Prefixes.Add(prefix);

                if (_host == "127.0.0.1" || _host == "localhost")
                {
                    // Adiciona localhost como alias se não estiver presente
                    var altPrefix = _host == "127.0.0.1" ? $"http://localhost:{_port}/" : $"http://127.0.0.1:{_port}/";
                    try
                    {
                        listener.Prefixes.Add(altPrefix);
                    }
                    catch
                    {
                        // Ignora se não puder adicionar prefixo alternativo
                    }
                }

                listener.Start();
                _listener = listener;
                _logger.LogInformation("[NEBULA-HTTP] Servidor HTTP de streaming iniciado em http://{Host}:{Port}/", _host, _port);
                _listenTask = ListenLoopAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                try
                {
                    listener?.Close();
                }
                catch
                {
                }

                _listener = null;
                _listenTask = null;
                _logger.LogWarning(ex, "[NEBULA-HTTP] Não foi possível iniciar o listener HTTP em {Host}:{Port}", _host, _port);
            }
        }
    }

    /// <summary>
    /// Encerra o listener HTTP.
    /// </summary>
    public void Stop()
    {
        lock (_lifecycleLock)
        {
            _cts.Cancel();
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
            }

            _listener = null;
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                // HandleRequestAsync already catches request-level failures. Starting it directly
                // avoids an unobserved Task.Run wrapper and still allows concurrent requests because
                // the method yields at its first asynchronous operation.
                _ = HandleRequestAsync(context, cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug(ex, "[NEBULA-HTTP] Erro ao aceitar conexão HTTP de stream.");
                }
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requestStarted = Stopwatch.GetTimestamp();
        var route = "other";

        try
        {
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
            var isStreamingRequest = path.Equals("/stream", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/transcode", StringComparison.OrdinalIgnoreCase);
            route = GetMetricRoute(path);
            requestCancellation.CancelAfter(isStreamingRequest ? StreamRequestTimeout : ControlRequestTimeout);
            var requestToken = requestCancellation.Token;
            var isHead = request.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase);
            response.Headers["Cache-Control"] = "no-store";
            response.Headers["Pragma"] = "no-cache";

            if (!IsAuthorized(request))
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Headers["WWW-Authenticate"] = "Bearer realm=nebula-stream";
                response.Close();
                return;
            }

            if (isStreamingRequest)
            {
                if (!await _streamConcurrency.WaitAsync(TimeSpan.FromSeconds(5), requestToken).ConfigureAwait(false))
                {
                    SaturatedRequestCounter.WithLabels(route).Inc();
                    response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    response.Headers["Retry-After"] = "5";
                    response.Close();
                    return;
                }

                try
                {
                    await HandleStreamRequestAsync(context, isHead, requestToken).ConfigureAwait(false);
                }
                finally
                {
                    _streamConcurrency.Release();
                }
            }
            else if (path.Equals("/play", StringComparison.OrdinalIgnoreCase))
            {
                await HandlePlayRequestAsync(context, isHead).ConfigureAwait(false);
            }
            else if (path.Equals("/api/files", StringComparison.OrdinalIgnoreCase))
            {
                await HandleListFilesRequestAsync(context, isHead, requestToken).ConfigureAwait(false);
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "text/plain; charset=utf-8";
                var text = Encoding.UTF8.GetBytes("Nebula HTTP Stream Server (MulletaFlix) ativo.");
                response.ContentLength64 = text.Length;
                if (!isHead)
                {
                    await response.OutputStream.WriteAsync(text, requestToken).ConfigureAwait(false);
                }

                response.Close();
            }
        }
        catch (HttpListenerException)
        {
            // Cliente desconectou normalmente durante o streaming
        }
        catch (OperationCanceledException)
        {
            // Cancelamento solicitado pelo servidor ou pelo timeout da requisição.
            // Em HttpListener, o fechamento do cliente também aparece como
            // HttpListenerException durante a escrita do corpo.
            try
            {
                response.Close();
            }
            catch
            {
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-HTTP] Erro ao processar requisição HTTP de stream: {Path}", request.Url?.PathAndQuery);
            try
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Close();
            }
            catch
            {
            }
        }
        finally
        {
            RequestCounter.WithLabels(route, response.StatusCode.ToString(CultureInfo.InvariantCulture)).Inc();
            RequestDuration.WithLabels(route).Observe(Stopwatch.GetElapsedTime(requestStarted).TotalSeconds);
        }
    }

    private static string GetMetricRoute(string path)
    {
        return path.ToLowerInvariant() switch
        {
            "/stream" => "stream",
            "/transcode" => "transcode",
            "/play" => "play",
            "/api/files" => "api_files",
            _ => "other"
        };
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        if (string.IsNullOrWhiteSpace(_streamToken))
        {
            return false;
        }

        var providedToken = request.Headers["Authorization"];
        if (!string.IsNullOrWhiteSpace(providedToken) &&
            providedToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            providedToken = providedToken[7..].Trim();
        }

        // Keep query-string compatibility for existing .strm clients. New clients
        // should send Authorization: Bearer so the token does not enter URL logs.
        if (string.IsNullOrWhiteSpace(providedToken))
        {
            providedToken = request.QueryString["token"];
        }
        if (string.IsNullOrWhiteSpace(providedToken))
        {
            return false;
        }

        return AreTokensEqual(_streamToken, providedToken);
    }

    private static bool AreTokensEqual(string expectedToken, string providedToken)
    {
        if (string.IsNullOrWhiteSpace(expectedToken) || string.IsNullOrWhiteSpace(providedToken))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(expectedToken);
        var provided = Encoding.UTF8.GetBytes(providedToken);
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private async Task HandleStreamRequestAsync(HttpListenerContext context, bool isHead, CancellationToken cancellationToken)
    {
        var request = context.Request;
        var response = context.Response;

        var fileIdParam = request.QueryString["id"];
        if (string.IsNullOrWhiteSpace(fileIdParam))
        {
            response.StatusCode = (int)HttpStatusCode.BadRequest;
            response.Close();
            return;
        }

        var doc = await _mongoContext.FindFileByVirtualPathOrNameAsync(fileIdParam, cancellationToken).ConfigureAwait(false);
        if (doc == null && ObjectId.TryParse(fileIdParam, out var objId))
        {
            doc = await _mongoContext.FindFileByIdAsync(objId, cancellationToken).ConfigureAwait(false);
        }

        if (doc == null)
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
            return;
        }

        var fileName = doc.TryGetValue("name", out var nVal) && nVal.IsString ? nVal.AsString : "media.bin";
        var (partsList, totalSize) = BuildStreamParts(doc);
        if (totalSize <= 0)
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
            return;
        }

        var contentType = GuessContentType(fileName);
        response.Headers.Set("Accept-Ranges", "bytes");
        response.Headers.Set("Content-Disposition", BuildContentDisposition(fileName));
        response.ContentType = contentType;

        var rangeHeader = request.Headers["Range"];
        if (!TryParseRange(rangeHeader, totalSize, out var start, out var end, out var isRange))
        {
            response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
            response.Headers.Set("Content-Range", $"bytes */{totalSize}");
            response.Close();
            return;
        }

        var contentLength = end - start + 1;
        response.ContentLength64 = contentLength;

        if (isRange)
        {
            response.StatusCode = (int)HttpStatusCode.PartialContent;
            response.Headers.Set("Content-Range", $"bytes {start}-{end}/{totalSize}");
        }
        else
        {
            response.StatusCode = (int)HttpStatusCode.OK;
        }

        if (isHead)
        {
            response.Close();
            return;
        }

        await using var stream = new NebulaChunkedStream(_telegramPool, partsList, totalSize, _logger, _playbackCache, GetMediaCacheKey(doc));
        stream.Seek(start, SeekOrigin.Begin);

        // Rented instead of allocated: a fresh 128 KB array per request means roughly one gigabyte of
        // garbage for every gigabyte streamed, on a component whose whole job is streaming.
        var buffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
        try
        {
            long remaining = contentLength;

            while (remaining > 0 && !cancellationToken.IsCancellationRequested)
            {
                var toRead = (int)Math.Min(StreamBufferSize, remaining);
                var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                await response.OutputStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        response.Close();
    }

    private static (List<NebulaStreamPart> Parts, long TotalSize) BuildStreamParts(BsonDocument doc)
    {
        var localPath = doc.TryGetValue("local_path", out var pathValue) && pathValue.IsString ? pathValue.AsString : null;
        var parts = new List<NebulaStreamPart>();
        if (doc.TryGetValue("parts", out var partsValue) && partsValue.IsBsonArray && partsValue.AsBsonArray.Count > 0)
        {
            foreach (var element in partsValue.AsBsonArray)
            {
                if (element is not BsonDocument partDoc)
                {
                    continue;
                }

                parts.Add(new NebulaStreamPart
                {
                    PartIndex = partDoc.Contains("part_number") ? partDoc.GetValue("part_number").ToInt32() : (partDoc.Contains("part_id") ? partDoc.GetValue("part_id").ToInt32() : parts.Count),
                    Size = partDoc.Contains("size") ? partDoc.GetValue("size").ToInt64() : (partDoc.Contains("file_size") ? partDoc.GetValue("file_size").ToInt64() : 16L * 1024L * 1024L),
                    FileId = partDoc.Contains("tg_file_id") ? partDoc.GetValue("tg_file_id").AsString : (partDoc.Contains("tg_file") ? partDoc.GetValue("tg_file").AsString : string.Empty),
                    BotIndex = partDoc.Contains("bot_index") ? partDoc.GetValue("bot_index").ToInt32() : -1,
                    ChatId = partDoc.Contains("tg_chat_id") ? partDoc.GetValue("tg_chat_id").ToInt64() : (partDoc.Contains("tg_chat") ? partDoc.GetValue("tg_chat").ToInt64() : 0L),
                    MessageId = partDoc.Contains("tg_message_id") ? checked((int)partDoc.GetValue("tg_message_id").ToInt64()) : (partDoc.Contains("tg_message") ? checked((int)partDoc.GetValue("tg_message").ToInt64()) : 0),
                    LocalPath = localPath
                });
            }

            long offset = 0;
            foreach (var part in parts.OrderBy(part => part.PartIndex))
            {
                part.FileOffset = offset;
                offset += part.Size;
            }
        }
        else
        {
            parts.Add(new NebulaStreamPart
            {
                PartIndex = 0,
                Size = doc.Contains("size") ? doc.GetValue("size").ToInt64() : (doc.Contains("file_size") ? doc.GetValue("file_size").ToInt64() : 0L),
                FileId = doc.Contains("tg_file_id") ? doc.GetValue("tg_file_id").AsString : (doc.Contains("tg_file") ? doc.GetValue("tg_file").AsString : string.Empty),
                BotIndex = doc.Contains("bot_index") ? doc.GetValue("bot_index").ToInt32() : -1,
                ChatId = doc.Contains("tg_chat_id") ? doc.GetValue("tg_chat_id").ToInt64() : (doc.Contains("tg_chat") ? doc.GetValue("tg_chat").ToInt64() : 0L),
                MessageId = doc.Contains("tg_message_id") ? checked((int)doc.GetValue("tg_message_id").ToInt64()) : (doc.Contains("tg_message") ? checked((int)doc.GetValue("tg_message").ToInt64()) : 0),
                LocalPath = localPath
            });
        }

        var totalSize = doc.Contains("size") ? doc.GetValue("size").ToInt64() : (doc.Contains("file_size") ? doc.GetValue("file_size").ToInt64() : parts.Sum(part => part.Size));
        return (parts, totalSize);
    }

    private static string GetMediaCacheKey(BsonDocument doc)
    {
        return doc.TryGetValue("_id", out var id) ? id.ToString() : doc.GetValue("name", "media.bin").AsString;
    }

    private static bool TryParseRange(string? rangeHeader, long totalSize, out long start, out long end, out bool isRange)
    {
        start = 0;
        end = totalSize - 1;
        isRange = false;

        if (string.IsNullOrWhiteSpace(rangeHeader))
        {
            return totalSize > 0;
        }

        if (totalSize <= 0 || !rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rangeValue = rangeHeader[6..].Trim();
        if (rangeValue.Length == 0 || rangeValue.Contains(',', StringComparison.Ordinal))
        {
            return false;
        }

        var hyphenIndex = rangeValue.IndexOf('-', StringComparison.Ordinal);
        if (hyphenIndex < 0 || rangeValue.IndexOf('-', hyphenIndex + 1) >= 0)
        {
            return false;
        }

        var startText = rangeValue[..hyphenIndex].Trim();
        var endText = rangeValue[(hyphenIndex + 1)..].Trim();
        if (startText.Length == 0)
        {
            if (!long.TryParse(endText, out var suffixLength) || suffixLength <= 0)
            {
                return false;
            }

            start = suffixLength >= totalSize ? 0 : totalSize - suffixLength;
            isRange = true;
            return true;
        }

        if (!long.TryParse(startText, out start) || start < 0 || start >= totalSize)
        {
            return false;
        }

        if (endText.Length > 0)
        {
            if (!long.TryParse(endText, out end) || end < start)
            {
                return false;
            }

            end = Math.Min(end, totalSize - 1);
        }

        isRange = true;
        return true;
    }

    private static string BuildContentDisposition(string fileName)
    {
        var safeName = new string((fileName ?? string.Empty)
            .Where(character => character >= ' ' && character != '"' && character != '\\' && character != '\u007f')
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "media.bin";
        }

        return $"inline; filename=\"{safeName}\"; filename*=UTF-8''{Uri.EscapeDataString(safeName)}";
    }

    private static async Task HandlePlayRequestAsync(HttpListenerContext context, bool isHead)
    {
        var request = context.Request;
        var response = context.Response;

        var fileId = request.QueryString["id"] ?? string.Empty;
        var token = request.QueryString["token"] ?? string.Empty;
        var encodedToken = WebUtility.UrlEncode(token);
        var encodedFileId = WebUtility.UrlEncode(fileId);
        var html = $@"<!DOCTYPE html>
<html lang=""pt-BR"">
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
    <title>Nebula Video Player</title>
    <style>
        body {{ margin: 0; background: #0b0c10; display: flex; align-items: center; justify-content: center; height: 100vh; font-family: system-ui, sans-serif; color: #fff; }}
        video {{ max-width: 95vw; max-height: 90vh; border-radius: 8px; box-shadow: 0 10px 30px rgba(0,0,0,0.8); outline: none; }}
    </style>
</head>
<body>
    <video controls autoplay playsinline>
        <source src=""/stream?id={encodedFileId}&amp;token={encodedToken}"">
        Seu navegador não suporta a tag de vídeo.
    </video>
</body>
</html>";

        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        response.Headers["Cache-Control"] = "no-store, private";
        response.Headers["Referrer-Policy"] = "no-referrer";

        if (!isHead)
        {
            await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        }

        response.Close();
    }

    private async Task HandleListFilesRequestAsync(HttpListenerContext context, bool isHead, CancellationToken cancellationToken)
    {
        var response = context.Response;
        var files = await _mongoContext.GetAllCompletedFilesAsync(cancellationToken).ConfigureAwait(false);

        var list = files.Select(doc => new
        {
            id = doc.Contains("_id") ? doc["_id"].ToString() : string.Empty,
            name = doc.GetValue("name", string.Empty).AsString,
            size = doc.Contains("size") ? doc.GetValue("size").ToInt64() : (doc.Contains("file_size") ? doc.GetValue("file_size").ToInt64() : 0L),
            parent = doc.Contains("parent") && !doc["parent"].IsBsonNull ? doc["parent"].ToString() : "/"
        }).ToList();

        var json = JsonSerializer.Serialize(list);
        var bytes = Encoding.UTF8.GetBytes(json);

        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;

        if (!isHead)
        {
            await response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        }

        response.Close();
    }

    private static string GuessContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp4" or ".m4v" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".ts" => "video/mp2t",
            ".mp3" => "audio/mpeg",
            ".aac" => "audio/aac",
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            ".srt" => "text/plain; charset=utf-8",
            ".vtt" => "text/vtt; charset=utf-8",
            _ => "application/octet-stream"
        };
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            Stop();
            if (_listenTask != null)
            {
                try
                {
                    await _listenTask.ConfigureAwait(false);
                }
                catch
                {
                }
            }

            _cts.Dispose();
            _streamConcurrency.Dispose();
            _disposed = true;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _cts.Dispose();
            _streamConcurrency.Dispose();
            _disposed = true;
        }
    }
}
