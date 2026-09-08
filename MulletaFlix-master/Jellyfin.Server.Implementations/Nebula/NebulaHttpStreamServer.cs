#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Servidor HTTP nativo em C# (porta 2123 por padrão para MulletaFlix) para streaming direto de mídias armazenadas no Telegram via Nebula.
/// Fornece suporte completo a HTTP 206 (Partial Content), cabeçalhos Range para busca instantânea (seeking) e fallback de reprodução.
/// </summary>
public sealed class NebulaHttpStreamServer : IAsyncDisposable, IDisposable
{
    private readonly NebulaMongoContext _mongoContext;
    private readonly NebulaTelegramPool _telegramPool;
    private readonly string _host;
    private readonly int _port;
    private readonly ILogger<NebulaHttpStreamServer> _logger;
    private readonly CancellationTokenSource _cts = new();

    private HttpListener? _listener;
    private Task? _listenTask;
    private bool _disposed;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaHttpStreamServer"/>.
    /// </summary>
    public NebulaHttpStreamServer(
        NebulaMongoContext mongoContext,
        NebulaTelegramPool telegramPool,
        string host,
        int port,
        ILogger<NebulaHttpStreamServer> logger)
    {
        _mongoContext = mongoContext ?? throw new ArgumentNullException(nameof(mongoContext));
        _telegramPool = telegramPool ?? throw new ArgumentNullException(nameof(telegramPool));
        _host = string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host;
        _port = port > 0 ? port : 2123;
        _logger = logger;
    }

    /// <summary>
    /// Inicia o listener HTTP na porta configurada.
    /// </summary>
    public void Start()
    {
        if (_listener != null && _listener.IsListening)
        {
            return;
        }

        try
        {
            _listener = new HttpListener();
            // 0.0.0.0 is a bind address, not a valid Host header for HttpListener.
            // Use the HTTP.sys wildcard so generated STRM URLs can be consumed from LAN clients.
            var prefixHost = _host is "0.0.0.0" or "*" or "+" ? "+" : _host;
            var prefix = $"http://{prefixHost}:{_port}/";
            _listener.Prefixes.Add(prefix);

            if (_host == "127.0.0.1" || _host == "localhost")
            {
                // Adiciona localhost como alias se não estiver presente
                var altPrefix = _host == "127.0.0.1" ? $"http://localhost:{_port}/" : $"http://127.0.0.1:{_port}/";
                try
                {
                    _listener.Prefixes.Add(altPrefix);
                }
                catch
                {
                    // Ignora se não puder adicionar prefixo alternativo
                }
            }

            _listener.Start();
            _logger.LogInformation("[NEBULA-HTTP] Servidor HTTP de streaming iniciado em http://{Host}:{Port}/", _host, _port);
            _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-HTTP] Não foi possível iniciar o listener HTTP em {Host}:{Port}", _host, _port);
        }
    }

    /// <summary>
    /// Encerra o listener HTTP.
    /// </summary>
    public void Stop()
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
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context, cancellationToken), cancellationToken);
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

        try
        {
            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
            var isHead = request.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase);

            if (path.Equals("/stream", StringComparison.OrdinalIgnoreCase) || path.Equals("/transcode", StringComparison.OrdinalIgnoreCase))
            {
                await HandleStreamRequestAsync(context, isHead, cancellationToken).ConfigureAwait(false);
            }
            else if (path.Equals("/play", StringComparison.OrdinalIgnoreCase))
            {
                await HandlePlayRequestAsync(context, isHead).ConfigureAwait(false);
            }
            else if (path.Equals("/api/files", StringComparison.OrdinalIgnoreCase))
            {
                await HandleListFilesRequestAsync(context, isHead, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = "text/plain; charset=utf-8";
                var text = Encoding.UTF8.GetBytes("Nebula HTTP Stream Server (MulletaFlix) ativo.");
                response.ContentLength64 = text.Length;
                if (!isHead)
                {
                    await response.OutputStream.WriteAsync(text, cancellationToken).ConfigureAwait(false);
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
            // Cancelamento solicitado
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
        var localPath = doc.TryGetValue("local_path", out var lpVal) && lpVal.IsString ? lpVal.AsString : null;

        var partsList = new List<NebulaStreamPart>();
        if (doc.TryGetValue("parts", out var partsValue) && partsValue.IsBsonArray && partsValue.AsBsonArray.Count > 0)
        {
            var partsArray = partsValue.AsBsonArray;
            foreach (var partElement in partsArray)
            {
                if (partElement is not BsonDocument partDoc)
                {
                    continue;
                }

                var partFileId = partDoc.Contains("tg_file_id") ? partDoc.GetValue("tg_file_id").AsString : (partDoc.Contains("tg_file") ? partDoc.GetValue("tg_file").AsString : string.Empty);
                var botIndex = partDoc.Contains("bot_index") ? partDoc.GetValue("bot_index").ToInt32() : -1;
                var chatId = partDoc.Contains("tg_chat_id") ? partDoc.GetValue("tg_chat_id").ToInt64() : (partDoc.Contains("tg_chat") ? partDoc.GetValue("tg_chat").ToInt64() : 0L);
                var messageId = partDoc.Contains("tg_message_id") ? checked((int)partDoc.GetValue("tg_message_id").ToInt64()) : (partDoc.Contains("tg_message") ? checked((int)partDoc.GetValue("tg_message").ToInt64()) : 0);
                var partNum = partDoc.Contains("part_number") ? partDoc.GetValue("part_number").ToInt32() : (partDoc.Contains("part_id") ? partDoc.GetValue("part_id").ToInt32() : partsList.Count);
                var partSize = partDoc.Contains("size") ? partDoc.GetValue("size").ToInt64() : (partDoc.Contains("file_size") ? partDoc.GetValue("file_size").ToInt64() : 16L * 1024L * 1024L);

                partsList.Add(new NebulaStreamPart
                {
                    PartIndex = partNum,
                    FileOffset = 0,
                    Size = partSize,
                    FileId = partFileId,
                    BotIndex = botIndex,
                    ChatId = chatId,
                    MessageId = messageId,
                    LocalPath = localPath
                });
            }

            long sortedOffset = 0;
            foreach (var part in partsList.OrderBy(part => part.PartIndex))
            {
                part.FileOffset = sortedOffset;
                sortedOffset += part.Size;
            }
        }
        else
        {
            var chatId = doc.Contains("tg_chat_id") ? doc.GetValue("tg_chat_id").ToInt64() : (doc.Contains("tg_chat") ? doc.GetValue("tg_chat").ToInt64() : 0L);
            var messageId = doc.Contains("tg_message_id") ? checked((int)doc.GetValue("tg_message_id").ToInt64()) : (doc.Contains("tg_message") ? checked((int)doc.GetValue("tg_message").ToInt64()) : 0);
            var fileId = doc.Contains("tg_file_id") ? doc.GetValue("tg_file_id").AsString : (doc.Contains("tg_file") ? doc.GetValue("tg_file").AsString : string.Empty);
            var botIndex = doc.Contains("bot_index") ? doc.GetValue("bot_index").ToInt32() : -1;
            var fileSize = doc.Contains("size") ? doc.GetValue("size").ToInt64() : (doc.Contains("file_size") ? doc.GetValue("file_size").ToInt64() : 0L);

            partsList.Add(new NebulaStreamPart
            {
                PartIndex = 0,
                FileOffset = 0,
                Size = fileSize,
                FileId = fileId,
                BotIndex = botIndex,
                ChatId = chatId,
                MessageId = messageId,
                LocalPath = localPath
            });
        }

        var totalSize = doc.Contains("size") ? doc.GetValue("size").ToInt64() : (doc.Contains("file_size") ? doc.GetValue("file_size").ToInt64() : partsList.Sum(p => p.Size));
        if (totalSize <= 0)
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
            return;
        }

        var contentType = GuessContentType(fileName);
        response.Headers.Set("Accept-Ranges", "bytes");
        response.Headers.Set("Content-Disposition", $"inline; filename=\"{Uri.EscapeDataString(fileName)}\"");
        response.ContentType = contentType;

        var rangeHeader = request.Headers["Range"];
        long start = 0;
        long end = totalSize - 1;
        var isRange = false;

        if (!string.IsNullOrWhiteSpace(rangeHeader) && rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            var rangeValue = rangeHeader[6..].Trim();
            var hyphenIdx = rangeValue.IndexOf('-', StringComparison.Ordinal);
            if (hyphenIdx >= 0)
            {
                var startStr = rangeValue[..hyphenIdx].Trim();
                var endStr = rangeValue[(hyphenIdx + 1)..].Trim();

                if (long.TryParse(startStr, out var parsedStart))
                {
                    start = parsedStart;
                    if (long.TryParse(endStr, out var parsedEnd))
                    {
                        end = Math.Min(parsedEnd, totalSize - 1);
                    }
                    isRange = true;
                }
                else if (long.TryParse(endStr, out var suffixLength))
                {
                    start = Math.Max(0, totalSize - suffixLength);
                    isRange = true;
                }
            }
        }

        if (start > end || start >= totalSize)
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

        await using var stream = new NebulaChunkedStream(_telegramPool, partsList, totalSize, _logger);
        stream.Seek(start, SeekOrigin.Begin);

        var buffer = new byte[64 * 1024];
        long remaining = contentLength;

        while (remaining > 0 && !cancellationToken.IsCancellationRequested)
        {
            var toRead = (int)Math.Min(buffer.Length, remaining);
            var read = await stream.ReadAsync(buffer.AsMemory(0, toRead), cancellationToken).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }

            await response.OutputStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            remaining -= read;
        }

        response.Close();
    }

    private static async Task HandlePlayRequestAsync(HttpListenerContext context, bool isHead)
    {
        var request = context.Request;
        var response = context.Response;

        var fileId = request.QueryString["id"] ?? string.Empty;
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
        <source src=""/stream?id={WebUtility.UrlEncode(fileId)}"">
        Seu navegador não suporta a tag de vídeo.
    </video>
</body>
</html>";

        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = (int)HttpStatusCode.OK;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;

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
            _disposed = true;
        }
    }
}
