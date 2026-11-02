using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MulletaFlix.Server;
using Polly.CircuitBreaker;
using Xunit;

namespace Jellyfin.Server.Tests;

public sealed class StartupHttpRetryPolicyTests
{
    [Fact]
    public async Task GetRetryPolicy_RetriesTransientServerError()
    {
        var responses = new List<HttpResponseMessage>();
        var attempts = 0;
        try
        {
            var response = await Startup.GetRetryPolicy().ExecuteAsync(() =>
            {
                attempts++;
                var result = new HttpResponseMessage(attempts == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK);
                responses.Add(result);
                return Task.FromResult(result);
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, attempts);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task GetRetryPolicy_DoesNotRetryNotFound()
    {
        var responses = new List<HttpResponseMessage>();
        var attempts = 0;
        try
        {
            var response = await Startup.GetRetryPolicy().ExecuteAsync(() =>
            {
                attempts++;
                var result = new HttpResponseMessage(HttpStatusCode.NotFound);
                responses.Add(result);
                return Task.FromResult(result);
            });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(1, attempts);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task GetRetryPolicy_RetriesIdempotentRequestAfterRetryAfterDelay()
    {
        var responses = new List<HttpResponseMessage>();
        var requests = new List<HttpRequestMessage>();
        var attempts = 0;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await Startup.GetRetryPolicy().ExecuteAsync(() =>
            {
                attempts++;
                var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/");
                requests.Add(request);
                var result = new HttpResponseMessage(attempts == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)
                {
                    RequestMessage = request
                };
                if (attempts == 1)
                {
                    result.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(System.TimeSpan.FromSeconds(1));
                }

                responses.Add(result);
                return Task.FromResult(result);
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, attempts);
            Assert.True(stopwatch.Elapsed >= System.TimeSpan.FromMilliseconds(900));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }

            foreach (var request in requests)
            {
                request.Dispose();
            }
        }
    }

    [Fact]
    public async Task GetRetryPolicy_UsesRetryAfterHttpDate()
    {
        var responses = new List<HttpResponseMessage>();
        var requests = new List<HttpRequestMessage>();
        var attempts = 0;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await Startup.GetRetryPolicy().ExecuteAsync(() =>
            {
                attempts++;
                var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/");
                requests.Add(request);
                var result = new HttpResponseMessage(attempts == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)
                {
                    RequestMessage = request
                };
                if (attempts == 1)
                {
                    result.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(System.DateTimeOffset.UtcNow.AddSeconds(1));
                }

                responses.Add(result);
                return Task.FromResult(result);
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, attempts);
            Assert.True(stopwatch.Elapsed >= System.TimeSpan.FromMilliseconds(900));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }

            foreach (var request in requests)
            {
                request.Dispose();
            }
        }
    }

    [Fact]
    public async Task GetRetryPolicy_UsesExponentialDelayWhenRetryAfterIsMissing()
    {
        var responses = new List<HttpResponseMessage>();
        var requests = new List<HttpRequestMessage>();
        var attempts = 0;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var response = await Startup.GetRetryPolicy().ExecuteAsync(() =>
            {
                attempts++;
                var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/");
                requests.Add(request);
                var result = new HttpResponseMessage(attempts == 1 ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK)
                {
                    RequestMessage = request
                };
                responses.Add(result);
                return Task.FromResult(result);
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, attempts);
            Assert.True(stopwatch.Elapsed >= System.TimeSpan.FromMilliseconds(1900));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }

            foreach (var request in requests)
            {
                request.Dispose();
            }
        }
    }

    [Fact]
    public async Task GetRetryPolicy_DoesNotRetryTooManyRequestsForPost()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://example.invalid/");
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            RequestMessage = request
        };
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(System.TimeSpan.Zero);
        var attempts = 0;
        try
        {
            var result = await Startup.GetRetryPolicy().ExecuteAsync(() =>
            {
                attempts++;
                return Task.FromResult(response);
            });

            Assert.Equal(HttpStatusCode.TooManyRequests, result.StatusCode);
            Assert.Equal(1, attempts);
        }
        finally
        {
            response.Dispose();
            request.Dispose();
        }
    }

    [Fact]
    public async Task GetRetryPolicy_DoesNotCountRateLimitsTowardCircuitBreaker()
    {
        var responses = new List<HttpResponseMessage>();
        var requests = new List<HttpRequestMessage>();
        var attempts = 0;
        var policy = Startup.GetRetryPolicy();
        try
        {
            for (var call = 0; call < 2; call++)
            {
                var response = await policy.ExecuteAsync(() =>
                {
                    attempts++;
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/");
                    requests.Add(request);
                    var result = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                    {
                        RequestMessage = request
                    };
                    result.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(System.TimeSpan.Zero);
                    responses.Add(result);
                    return Task.FromResult(result);
                });

                Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
            }

            Assert.Equal(8, attempts);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }

            foreach (var request in requests)
            {
                request.Dispose();
            }
        }
    }

    [Fact]
    public async Task GetRetryPolicy_CircuitBreakerCountsUnderlyingAttemptsAndShortCircuits()
    {
        var policy = Startup.GetRetryPolicy();
        var responses = new List<HttpResponseMessage>();
        var attempts = 0;
        HttpResponseMessage ServiceUnavailable()
        {
            attempts++;
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            responses.Add(response);
            return response;
        }

        try
        {
            var firstResponse = await policy.ExecuteAsync(() => Task.FromResult(ServiceUnavailable()));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, firstResponse.StatusCode);
            Assert.Equal(4, attempts);

            await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(
                () => policy.ExecuteAsync(() => Task.FromResult(ServiceUnavailable())));

            Assert.Equal(5, attempts);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task GetRetryPolicy_CircuitBreakerAllowsRequestAfterBreakDuration()
    {
        var policy = Startup.GetRetryPolicy();
        var responses = new List<HttpResponseMessage>();
        var attempts = 0;
        HttpResponseMessage ServiceUnavailable()
        {
            attempts++;
            var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            responses.Add(response);
            return response;
        }

        try
        {
            var firstResponse = await policy.ExecuteAsync(() => Task.FromResult(ServiceUnavailable()));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, firstResponse.StatusCode);

            await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(
                () => policy.ExecuteAsync(() => Task.FromResult(ServiceUnavailable())));
            Assert.Equal(5, attempts);

            await Task.Delay(System.TimeSpan.FromSeconds(31), TestContext.Current.CancellationToken);

            var recoveredResponse = await policy.ExecuteAsync(() =>
            {
                attempts++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                responses.Add(response);
                return Task.FromResult(response);
            });

            Assert.Equal(HttpStatusCode.OK, recoveredResponse.StatusCode);
            Assert.Equal(6, attempts);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }
}
