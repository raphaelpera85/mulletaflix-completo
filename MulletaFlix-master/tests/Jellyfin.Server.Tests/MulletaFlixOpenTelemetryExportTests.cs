using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Server.Implementations.FullSystemBackup;
using MulletaFlix.Server.Implementations.Nebula;
using MulletaFlix.Server.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace Jellyfin.Server.Tests;

/// <summary>
/// Valida o pipeline de exportação OTLP de ponta a ponta: o SDK OpenTelemetry real
/// é construído a partir de <see cref="MulletaFlixOpenTelemetryExtensions"/>, spans e métricas
/// são emitidos pelas fontes de produção e um receptor OTLP/HTTP em processo confirma
/// que os bytes chegaram codificados. Isto substitui a inspeção local por
/// <c>ActivityListener</c> como prova de que os spans realmente saem do processo,
/// sem depender de um collector externo instalado na máquina.
/// </summary>
public sealed class MulletaFlixOpenTelemetryExportTests
{
    private const string TracesPath = "/v1/traces";
    private const string MetricsPath = "/v1/metrics";

    [Fact]
    public async Task ConfigureOpenTelemetry_ExportsNebulaSpansOverOtlpHttpWithoutSensitivePayload()
    {
        using var receiver = new OtlpTraceReceiver(requireApiKey: true);
        receiver.Start();

        var previousEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var previousTracesEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
        var previousMetricsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT");
        var previousProtocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
        var previousHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
        var previousTracesHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS");
        var previousMetricsHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS");
        try
        {
            // Apenas o ambiente deste processo de teste é alterado; nenhum servidor
            // externo é iniciado nem reconfigurado.
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", receiver.Endpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", "X-API-Key=unit-test-secret");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS", null);

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
                Assert.Equal("unit-test-secret", payload.ApiKey);
                Assert.Equal(HttpStatusCode.OK, payload.StatusCode);
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
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", previousTracesEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", previousMetricsEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", previousProtocol);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", previousHeaders);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS", previousTracesHeaders);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS", previousMetricsHeaders);
        }
    }

    [Fact]
    public async Task ConfigureOpenTelemetry_ExportsBackupMetricsWithoutFilePaths()
    {
        using var receiver = new OtlpTraceReceiver(requireApiKey: true);
        receiver.Start();

        var previousEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        var previousTracesEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT");
        var previousMetricsEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT");
        var previousProtocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
        var previousHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS");
        var previousTracesHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS");
        var previousMetricsHeaders = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS");
        var privatePath = Path.Combine(Path.GetTempPath(), $"backup-private-path-{Guid.NewGuid():N}.zip");
        try
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", receiver.Endpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", "X-API-Key=unit-test-secret");
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS", null);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS", null);

            var services = new ServiceCollection();
            var signals = MulletaFlixOpenTelemetryExtensions.ConfigureOpenTelemetry(services, Environment.GetEnvironmentVariable);
            Assert.True(signals.MetricsEnabled);

            await using var provider = services.BuildServiceProvider();
            var meterProvider = provider.GetRequiredService<MeterProvider>();
            var backupService = new BackupService(
                NullLogger<BackupService>.Instance,
                null!,
                null!,
                null!,
                null!,
                null!);

            await Assert.ThrowsAsync<FileNotFoundException>(() => backupService.RestoreBackupAsync(privatePath));
            using var supabaseSync = new NebulaSupabaseSyncService(
                null!,
                NullLogger<NebulaSupabaseSyncService>.Instance);
            var syncResult = await supabaseSync.PerformBackupAsync(
                "https://private-supabase.invalid",
                "sb_publishable_private-test-key",
                progressAction: null,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(syncResult.Success);
            await using var nebulaManager = new NebulaFtpManager(
                null!,
                NullLogger<NebulaFtpManager>.Instance,
                NullLoggerFactory.Instance);
            var cleanupMethod = typeof(NebulaFtpManager).GetMethod(
                "RunCleanupCycleAsync",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var cleanupTask = (Task)cleanupMethod.Invoke(
                nebulaManager,
                [new List<string>(), CancellationToken.None])!;
            await cleanupTask;
            Assert.True(meterProvider.ForceFlush(10000), "O MeterProvider não conseguiu drenar as métricas.");

            var payloads = await receiver.WaitForRequestsAsync(1, TimeSpan.FromSeconds(30));
            var metricsPayload = Assert.Single(payloads);
            Assert.Equal("POST", metricsPayload.Method);
            Assert.Equal(MetricsPath, metricsPayload.Path);
            Assert.Equal("application/x-protobuf", metricsPayload.ContentType);
            Assert.Equal("unit-test-secret", metricsPayload.ApiKey);
            Assert.Equal(HttpStatusCode.OK, metricsPayload.StatusCode);

            var encodedPayload = Latin1(metricsPayload.Body);
            Assert.Contains("mulletaflix.backup.operations", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.backup.operation.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.supabase.operations", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.supabase.operation.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.nebula.cleanup.cycles", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("mulletaflix.nebula.cleanup.duration", encodedPayload, StringComparison.Ordinal);
            Assert.Contains("MulletaFlix.Server", encodedPayload, StringComparison.Ordinal);
            Assert.DoesNotContain(privatePath, encodedPayload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("private-supabase.invalid", encodedPayload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("sb_publishable_private-test-key", encodedPayload, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", previousEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", previousTracesEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_ENDPOINT", previousMetricsEndpoint);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL", previousProtocol);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", previousHeaders);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_TRACES_HEADERS", previousTracesHeaders);
            Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_METRICS_HEADERS", previousMetricsHeaders);
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

            await Task.Delay(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
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

    private sealed record CapturedRequest(string Method, string Path, string? ContentType, string? ApiKey, HttpStatusCode StatusCode, byte[] Body);

    private sealed class OtlpTraceReceiver : IDisposable
    {
        private const string RequiredApiKey = "unit-test-secret";
        private readonly HttpListener _listener = new();
        private readonly List<CapturedRequest> _received = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly CancellationTokenSource _cts = new();
        private readonly object _gate = new();

        public OtlpTraceReceiver(bool requireApiKey = false)
        {
            Port = GetFreePort();
            RequireApiKey = requireApiKey;
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        }

        public int Port { get; }

        public string Endpoint => $"http://127.0.0.1:{Port}";

        private bool RequireApiKey { get; }

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
                        context.Request.Headers["X-API-Key"],
                        RequireApiKey && !string.Equals(context.Request.Headers["X-API-Key"], RequiredApiKey, StringComparison.Ordinal)
                            ? HttpStatusCode.Unauthorized
                            : HttpStatusCode.OK,
                        buffer.ToArray());

                    lock (_gate)
                    {
                        _received.Add(captured);
                    }

                    // Coletores protegidos rejeitam chave ausente/incorreta.
                    // Em sucesso, resposta OTLP/HTTP vazia é válida.
                    context.Response.StatusCode = (int)captured.StatusCode;
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
