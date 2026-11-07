using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.DependencyInjection;
using MulletaFlix.Networking.HappyEyeballs;
using MulletaFlix.Server;
using Polly.CircuitBreaker;
using Xunit;

namespace Jellyfin.Server.Tests;

public sealed class StartupHttpClientRegistrationTests
{
    [Fact]
    public async Task HappyEyeballsConnectCallback_ConnectsThroughTlsInterceptingProxy()
    {
        const string targetHost = "unresolvable.invalid";
        using var rsa = RSA.Create(2048);
        var certificateRequest = new CertificateRequest(
            $"CN={targetHost}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName(targetHost);
        certificateRequest.CertificateExtensions.Add(subjectAlternativeNames.Build());
        using var generatedCertificate = certificateRequest.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(10));
        using var certificate = X509CertificateLoader.LoadPkcs12(
            generatedCertificate.Export(X509ContentType.Pfx),
            password: null,
            keyStorageFlags: X509KeyStorageFlags.UserKeySet);

        using var proxy = new TcpListener(IPAddress.Loopback, 0);
        proxy.Start();
        var proxyEndpoint = (IPEndPoint)proxy.LocalEndpoint;
        using var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy($"http://127.0.0.1:{proxyEndpoint.Port}"),
            ConnectCallback = HttpClientExtension.OnConnect,
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };
        using var httpClient = new HttpClient(handler);
        var proxyTask = ServeTlsInterceptingProxyAsync(proxy, certificate, TestContext.Current.CancellationToken);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(
                $"https://{targetHost}/media", TestContext.Current.CancellationToken);
        }
        catch
        {
            await proxyTask;
            throw;
        }

        using (response)
        {
            var (connectRequestLine, requestLine) = await proxyTask;

            Assert.Equal($"CONNECT {targetHost}:443 HTTP/1.1", connectRequestLine);
            Assert.Equal("GET /media HTTP/1.1", requestLine);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task HappyEyeballsConnectCallback_ConnectsThroughConfiguredProxy()
    {
        using var proxy = new TcpListener(IPAddress.Loopback, 0);
        proxy.Start();
        var proxyEndpoint = (IPEndPoint)proxy.LocalEndpoint;
        var acceptTask = proxy.AcceptTcpClientAsync(TestContext.Current.CancellationToken).AsTask();

        using var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy($"http://127.0.0.1:{proxyEndpoint.Port}"),
            ConnectCallback = HttpClientExtension.OnConnect
        };
        using var httpClient = new HttpClient(handler);

        var responseTask = httpClient.GetAsync("http://unresolvable.invalid/media", TestContext.Current.CancellationToken);
        using var proxyClient = await acceptTask;
        using var stream = proxyClient.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(TestContext.Current.CancellationToken)))
        {
        }

        var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(response, TestContext.Current.CancellationToken);
        using var httpResponse = await responseTask;

        Assert.Equal("GET http://unresolvable.invalid/media HTTP/1.1", requestLine);
        Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
    }

    [Fact]
    public async Task HappyEyeballsConnectCallback_AwaitsIpv4AfterIpv6RejectsIpv4Endpoint()
    {
        var previousUseIpv6 = HttpClientExtension.UseIPv6;
        HttpClientExtension.UseIPv6 = true;

        try
        {
            using var proxy = new TcpListener(IPAddress.Loopback, 0);
            proxy.Start();
            var proxyEndpoint = (IPEndPoint)proxy.LocalEndpoint;
            var acceptTask = proxy.AcceptTcpClientAsync(TestContext.Current.CancellationToken).AsTask();

            using var handler = new SocketsHttpHandler
            {
                Proxy = new WebProxy($"http://127.0.0.1:{proxyEndpoint.Port}"),
                ConnectCallback = HttpClientExtension.OnConnect
            };
            using var httpClient = new HttpClient(handler);

            var responseTask = httpClient.GetAsync("http://unresolvable.invalid/media", TestContext.Current.CancellationToken);
            using var proxyClient = await acceptTask;
            using var stream = proxyClient.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            _ = await reader.ReadLineAsync(TestContext.Current.CancellationToken);
            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(TestContext.Current.CancellationToken)))
            {
            }

            await stream.WriteAsync(
                "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(),
                TestContext.Current.CancellationToken);
            using var response = await responseTask;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            HttpClientExtension.UseIPv6 = previousUseIpv6;
        }
    }

    [Theory]
    [InlineData(NamedClient.Default)]
    [InlineData(NamedClient.MusicBrainz)]
    [InlineData(NamedClient.DirectIp)]
    public async Task RegisteredClient_RetriesLocalRateLimitResponse(string clientName)
    {
        using var server = new RateLimitResponseServer();
        server.Start();

        var services = new ServiceCollection();
        services.AddLogging();
        Startup.AddHttpClients(
            services,
            new ProductInfoHeaderValue("Mulletaflix", "1.0"),
            "https://example.invalid/");

        using var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(clientName);

        using var response = await httpClient.GetAsync(server.Endpoint, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, server.RequestCount);
    }

    [Theory]
    [InlineData(NamedClient.Default)]
    [InlineData(NamedClient.MusicBrainz)]
    [InlineData(NamedClient.DirectIp)]
    public async Task RegisteredClient_DoesNotRetryPostAfterTransientServerError(string clientName)
    {
        using var server = new RateLimitResponseServer(HttpStatusCode.ServiceUnavailable);
        server.Start();

        var services = new ServiceCollection();
        services.AddLogging();
        Startup.AddHttpClients(
            services,
            new ProductInfoHeaderValue("Mulletaflix", "1.0"),
            "https://example.invalid/");

        using var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(clientName);

        using var response = await httpClient.PostAsync(
            server.Endpoint,
            new StringContent("payload"),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(1, server.RequestCount);
    }

    [Fact]
    public async Task RegisteredClient_SharesCircuitBreakerAcrossRequests()
    {
        using var server = new RateLimitResponseServer(HttpStatusCode.ServiceUnavailable, alwaysUseFirstResponseStatus: true);
        server.Start();

        var services = new ServiceCollection();
        services.AddLogging();
        Startup.AddHttpClients(
            services,
            new ProductInfoHeaderValue("Mulletaflix", "1.0"),
            "https://example.invalid/");

        using var serviceProvider = services.BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(NamedClient.Default);

        for (var i = 0; i < 5; i++)
        {
            using var response = await httpClient.PostAsync(
                server.Endpoint,
                new StringContent("payload"),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(() => httpClient.PostAsync(
            server.Endpoint,
            new StringContent("payload"),
            TestContext.Current.CancellationToken));

        Assert.Equal(5, server.RequestCount);
    }

    private static async Task<(string? ConnectRequestLine, string? RequestLine)> ServeTlsInterceptingProxyAsync(
        TcpListener proxy,
        X509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        using var proxyClient = await proxy.AcceptTcpClientAsync(cancellationToken);
        using var networkStream = proxyClient.GetStream();
        var connectRequestLine = await ReadRequestLineAsync(networkStream, cancellationToken);
        var established = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
        await networkStream.WriteAsync(established, cancellationToken);

        using var tlsStream = new SslStream(networkStream, leaveInnerStreamOpen: true);
        await tlsStream.AuthenticateAsServerAsync(
            new SslServerAuthenticationOptions { ServerCertificate = certificate }, cancellationToken);
        var requestLine = await ReadRequestLineAsync(tlsStream, cancellationToken);
        var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        await tlsStream.WriteAsync(response, cancellationToken);

        return (connectRequestLine, requestLine);
    }

    private static async Task<string?> ReadRequestLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken)))
        {
        }

        return requestLine;
    }

    private sealed class RateLimitResponseServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly HttpStatusCode _firstResponseStatus;
        private readonly bool _alwaysUseFirstResponseStatus;
        private int _requestCount;

        public RateLimitResponseServer(
            HttpStatusCode firstResponseStatus = HttpStatusCode.TooManyRequests,
            bool alwaysUseFirstResponseStatus = false)
        {
            _firstResponseStatus = firstResponseStatus;
            _alwaysUseFirstResponseStatus = alwaysUseFirstResponseStatus;
            using var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            Endpoint = $"http://127.0.0.1:{port}/";
        }

        public string Endpoint { get; }

        public int RequestCount => Volatile.Read(ref _requestCount);

        public void Start()
        {
            _listener.Start();
            _ = RespondAsync();
        }

        private async Task RespondAsync()
        {
            try
            {
                while (_listener.IsListening)
                {
                    var context = await _listener.GetContextAsync();
                    var requestNumber = Interlocked.Increment(ref _requestCount);
                    context.Response.StatusCode = requestNumber == 1 || _alwaysUseFirstResponseStatus
                        ? (int)_firstResponseStatus
                        : (int)HttpStatusCode.OK;
                    if (requestNumber == 1 && _firstResponseStatus == HttpStatusCode.TooManyRequests)
                    {
                        context.Response.Headers["Retry-After"] = "0";
                    }

                    context.Response.ContentLength64 = 0;
                    context.Response.Close();
                }
            }
            catch (HttpListenerException)
            {
                // The listener is closed when the test finishes.
            }
            finally
            {
                _stopped.TrySetResult();
            }
        }

        public void Dispose()
        {
            _listener.Close();
            _stopped.Task.GetAwaiter().GetResult();
        }
    }
}
