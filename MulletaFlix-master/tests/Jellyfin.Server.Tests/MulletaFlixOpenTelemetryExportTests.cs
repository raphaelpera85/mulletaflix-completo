using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.DependencyInjection;
using MulletaFlix.Server.Extensions;
using OpenTelemetry.Trace;
using Xunit;

namespace Jellyfin.Server.Tests;

/// <summary>
/// Valida o pipeline de exportação OTLP de ponta a ponta: o SDK OpenTelemetry real
/// é construído a partir de <see cref="MulletaFlixOpenTelemetryExtensions"/>, os spans
/// são emitidos pelas fontes de produção e um receptor OTLP/HTTP em processo confirma
/// que os bytes chegaram codificados. Isto substitui a inspeção local por
/// <c>ActivityListener</c> como prova de que os spans realmente saem do processo,
/// sem depender de um collector externo instalado na máquina.
/// </summary>
public sealed class MulletaFlixOpenTelemetryExportTests
{
    private const string TracesPath = "/v1/traces";

    [Fact]
    public async Task ConfigureOpenTelemetry_ExportsNebulaSpansOverOtlpHttpWithoutSensitivePayload()
    {
        using var receiver = new OtlpTraceReceiver();
        receiver.Start();

        var previousEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var previousProtocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
        try
        {
            // Apenas o ambiente deste processo de teste é alterado; nenhum servidor
            // externo é iniciado nem reconfigurado.
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", receiver.Endpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");

            var services = new ServiceCollection();
            var signals = MulletaFlixOpenTelemetryExtensions.ConfigureOpenTelemetry(
                services,
                Environment.GetEnvironmentVariable);

            Assert.True(signals.TracesEnabled);

            await using var provider = services.BuildServiceProvider();
            var tracerProvider = provider.GetRequiredService<TracerProvider>();

            // Emite pelas fontes de produção realmente registradas no pipeline.
            EmitSpanFrom(
                NebulaPlaybackSessionMonitor.ActivitySourceName,
                "nebula.playback.session_start",
                ActivityKind.Internal,
                activity =>
                {
                    activity.SetTag("nebula.playback.result", "tracked");
                    activity.SetTag("nebula.playback.active_sessions", 1);
                });

            EmitSpanFrom(
                NebulaMongoContext.ActivitySourceName,
                "mongodb.sync_staging_directory",
                ActivityKind.Client,
                activity =>
                {
                    activity.SetTag("mongodb.result", "success");
                    activity.SetTag("nebula.staging.root_count", 2);
                });

            EmitSpanFrom(
                NebulaTelegramPool.ActivitySourceName,
                "telegram.send_message",
                ActivityKind.Client,
                activity => activity.SetTag("telegram.result", "success"));

            Assert.True(tracerProvider.ForceFlush(10000), "O TracerProvider não conseguiu drenar os spans.");

            var payloads = await receiver.WaitForRequestsAsync(1, TimeSpan.FromSeconds(30));
            Assert.NotEmpty(payloads);

            // O exporter OTLP/HTTP precisa ter falado o protocolo correto no caminho correto.
            Assert.All(payloads, payload =>
            {
                Assert.Equal("POST", payload.Method);
                Assert.Equal(TracesPath, payload.Path);
                Assert.Equal("application/x-protobuf", payload.ContentType);
                Assert.NotEmpty(payload.Body);
            });

            var combined = string.Concat(payloads.Select(payload => Latin1(payload.Body)));

            // Os nomes de operação chegaram ao receptor, ou seja, saíram do processo.
            Assert.Contains("nebula.playback.session_start", combined, StringComparison.Ordinal);
            Assert.Contains("mongodb.sync_staging_directory", combined, StringComparison.Ordinal);
            Assert.Contains("telegram.send_message", combined, StringComparison.Ordinal);
            Assert.Contains("MulletaFlix.Server", combined, StringComparison.Ordinal);

            // E o payload exportado não carrega nada sensível.
            foreach (var forbidden in new[]
            {
                "Sensitive",
                "mongodb://",
                "chat_id",
                "bot_token",
                "connectionString",
                "playSessionId"
            })
            {
                Assert.DoesNotContain(forbidden, combined, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", previousEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", previousProtocol);
        }
    }

    [Fact]
    public async Task ConfigureOpenTelemetry_WithoutEndpointDoesNotExportAnything()
    {
        using var receiver = new OtlpTraceReceiver();
        receiver.Start();

        var previousEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var previousTraces = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
        var previousMetrics = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT");
        try
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", null);

            var services = new ServiceCollection();
            var signals = MulletaFlixOpenTelemetryExtensions.ConfigureOpenTelemetry(
                services,
                Environment.GetEnvironmentVariable);

            Assert.False(signals.TracesEnabled);
            Assert.False(signals.MetricsEnabled);

            await using var provider = services.BuildServiceProvider();
            Assert.Null(provider.GetService<TracerProvider>());

            // Sem pipeline não há listener registrado, então o próprio
            // StartActivity devolve null: o custo de telemetria é zero.
            using var source = new ActivitySource(NebulaPlaybackSessionMonitor.ActivitySourceName);
            using var activity = source.StartActivity("nebula.playback.session_start", ActivityKind.Internal);
            Assert.Null(activity);

            await Task.Delay(TimeSpan.FromSeconds(2));
            Assert.Empty(receiver.Received);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", previousEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", previousTraces);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", previousMetrics);
        }
    }

    private static void EmitSpanFrom(
        string sourceName,
        string operationName,
        ActivityKind kind,
        Action<Activity> configure)
    {
        using var source = new ActivitySource(sourceName);
        using var activity = source.StartActivity(operationName, kind);
        Assert.NotNull(activity);
        configure(activity);
    }

    private static string Latin1(byte[] body)
    {
        // Latin-1 nunca falha e preserva os bytes 1:1, permitindo procurar as
        // strings UTF-8 embutidas no protobuf sem precisar decodificá-lo.
        return Encoding.Latin1.GetString(body);
    }

    private sealed record CapturedRequest(string Method, string Path, string? ContentType, byte[] Body);

    private sealed class OtlpTraceReceiver : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly List<CapturedRequest> _received = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly CancellationTokenSource _cts = new();
        private readonly object _gate = new();

        public OtlpTraceReceiver()
        {
            Port = GetFreePort();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        }

        public int Port { get; }

        public string Endpoint => $"http://127.0.0.1:{Port}";

        public IReadOnlyList<CapturedRequest> Received
        {
            get
            {
                lock (_gate)
                {
                    return _received.ToList();
                }
            }
        }

        public void Start()
        {
            _listener.Start();
            _ = Task.Run(AcceptLoopAsync);
        }

        public async Task<IReadOnlyList<CapturedRequest>> WaitForRequestsAsync(int count, TimeSpan timeout)
        {
            using var timeoutCts = new CancellationTokenSource(timeout);
            for (var i = 0; i < count; i++)
            {
                await _signal.WaitAsync(timeoutCts.Token);
            }

            return Received;
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                try
                {
                    using var buffer = new MemoryStream();
                    await context.Request.InputStream.CopyToAsync(buffer);
                    var captured = new CapturedRequest(
                        context.Request.HttpMethod,
                        context.Request.Url?.AbsolutePath ?? string.Empty,
                        context.Request.ContentType,
                        buffer.ToArray());

                    lock (_gate)
                    {
                        _received.Add(captured);
                    }

                    // Resposta OTLP/HTTP de sucesso: ExportTraceServiceResponse vazio.
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/x-protobuf";
                    context.Response.ContentLength64 = 0;
                    context.Response.Close();

                    _signal.Release();
                }
                catch (Exception)
                {
                    try
                    {
                        context.Response.Abort();
                    }
                    catch (Exception)
                    {
                        // O exporter tratará a falha de transporte; o teste não depende disto.
                    }
                }
            }
        }

        private static int GetFreePort()
        {
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        public void Dispose()
        {
            _cts.Cancel();
            if (_listener.IsListening)
            {
                _listener.Stop();
            }

            ((IDisposable)_listener).Dispose();
            _cts.Dispose();
            _signal.Dispose();
        }
    }
}
