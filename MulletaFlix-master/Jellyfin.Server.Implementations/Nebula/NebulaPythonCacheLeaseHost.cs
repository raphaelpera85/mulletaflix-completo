using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>Exposes playback cache leases to the supervised Python stream worker over loopback.</summary>
public sealed class NebulaPythonCacheLeaseHost : IAsyncDisposable
{
    private readonly NebulaPlaybackCacheAccessor _cacheAccessor;
    private readonly ILogger _logger;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentDictionary<string, IDisposable> _leases = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stop = new();
    private Task? _acceptTask;

    public NebulaPythonCacheLeaseHost(NebulaPlaybackCacheAccessor cacheAccessor, ILogger logger)
    {
        _cacheAccessor = cacheAccessor;
        _logger = logger;
        Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    public int Port { get; private set; }

    public string Token { get; }

    public void Start()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptTask = AcceptLoopAsync(_stop.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        await using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true))
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true })
        {
            string? connectionLeaseId = null;
            try
            {
                while (!cancellationToken.IsCancellationRequested && await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
                {
                    using var request = JsonDocument.Parse(line);
                    var root = request.RootElement;
                    var token = root.TryGetProperty("token", out var tokenValue) ? tokenValue.GetString() : null;
                    if (!TokensEqual(Token, token))
                    {
                        await writer.WriteLineAsync("{\"ok\":false}").ConfigureAwait(false);
                        return;
                    }

                    var action = root.TryGetProperty("action", out var actionValue) ? actionValue.GetString() : null;
                    if (string.Equals(action, "acquire", StringComparison.Ordinal)
                        && root.TryGetProperty("mediaId", out var idValue)
                        && idValue.GetString() is { Length: 24 } mediaId
                        && ObjectIdTextIsValid(mediaId))
                    {
                        var leaseId = Guid.NewGuid().ToString("N");
                        var (_, lease) = _cacheAccessor.Acquire("mongo:" + mediaId);
                        if (lease != null && _leases.TryAdd(leaseId, lease))
                        {
                            connectionLeaseId = leaseId;
                            await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = true, leaseId })).ConfigureAwait(false);
                        }
                        else
                        {
                            lease?.Dispose();
                            await writer.WriteLineAsync("{\"ok\":false}").ConfigureAwait(false);
                        }
                    }
                    else if (string.Equals(action, "release", StringComparison.Ordinal)
                        && root.TryGetProperty("leaseId", out var leaseValue)
                        && leaseValue.GetString() is { } leaseId
                        && _leases.TryRemove(leaseId, out var lease))
                    {
                        lease.Dispose();
                        connectionLeaseId = null;
                        await writer.WriteLineAsync("{\"ok\":true}").ConfigureAwait(false);
                    }
                    else
                    {
                        await writer.WriteLineAsync("{\"ok\":false}").ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException or OperationCanceledException or ObjectDisposedException)
            {
                _logger.LogDebug(ex, "Worker Python encerrou canal de lease do cache.");
            }
            finally
            {
                if (connectionLeaseId != null && _leases.TryRemove(connectionLeaseId, out var lease))
                {
                    lease.Dispose();
                }
            }
        }
    }

    private static bool TokensEqual(string expected, string? provided)
    {
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
    }

    private static bool ObjectIdTextIsValid(string value)
    {
        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask.ConfigureAwait(false);
            }
            catch (SocketException)
            {
                // Listener was closed to interrupt AcceptTcpClientAsync.
            }
        }

        foreach (var lease in _leases.Values)
        {
            lease.Dispose();
        }

        _leases.Clear();
        _stop.Dispose();
    }
}
