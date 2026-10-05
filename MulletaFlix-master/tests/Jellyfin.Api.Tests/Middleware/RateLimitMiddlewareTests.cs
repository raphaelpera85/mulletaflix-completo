using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MulletaFlix.Api.Tests.Middleware;

public sealed class RateLimitMiddlewareTests
{
    [Theory]
    [InlineData("/Users/Authenticate", true)]
    [InlineData("/Users/Authenticate/", true)]
    [InlineData("/Users/Register/reset", true)]
    [InlineData("/Users/AuthenticateFake", false)]
    [InlineData("/Users/Register2", false)]
    public void IsPathOrDescendant_RequiresRouteBoundary(string path, bool expected)
    {
        Assert.Equal(expected, RateLimitMiddleware.IsPathOrDescendant(path, "/Users/Authenticate")
            || RateLimitMiddleware.IsPathOrDescendant(path, "/Users/Register"));
    }

    [Theory]
    [InlineData("/assets/index.js", true)]
    [InlineData("/web/assets/index.js", true)]
    [InlineData("/serviceworker.js", true)]
    [InlineData("/config.json", true)]
    [InlineData("/Users/Authenticate", false)]
    [InlineData("/web/ConfigurationPages", false)]
    public void IsStaticWebAssetPath_DoesNotIncludeApiRoutes(string path, bool expected)
    {
        Assert.Equal(expected, RateLimitMiddleware.IsStaticWebAssetPath(path));
    }

    [Theory]
    [InlineData("/System/Info/Public", true)]
    [InlineData("/System/Info/Public/", true)]
    [InlineData("/Users/Public", true)]
    [InlineData("/System/Info/Publicity", false)]
    [InlineData("/System/Configuration", false)]
    public void PublicBootstrapPath_RequiresRouteBoundary(string path, bool expected)
    {
        Assert.Equal(expected, RateLimitMiddleware.IsPublicBootstrapPath(path));
    }

    [Theory]
    [InlineData("/media", "/media", true)]
    [InlineData("/media/", "/media", true)]
    [InlineData("/media/items", "/media", true)]
    [InlineData("/mediaBackup", "/media", false)]
    public void BaseUrl_IsPathUnderBaseUrl_RequiresPrefixBoundary(string path, string prefix, bool expected)
    {
        Assert.Equal(expected, BaseUrlRedirectionMiddleware.IsPathUnderBaseUrl(path, prefix));
    }

    [Theory]
    [InlineData("/Search/Hints", "search")]
    [InlineData("/Items/RemoteSearch", "search")]
    [InlineData("/Catalog/Audit", "administration")]
    [InlineData("/Catalog/Audit/Fix", "administration")]
    [InlineData("/Items/00000000-0000-0000-0000-000000000001/Refresh", "administration")]
    [InlineData("/Items/Refresh", null)]
    [InlineData("/Items/not-a-guid/Refresh", null)]
    [InlineData("/Items/00000000-0000-0000-0000-000000000000/Refresh", null)]
    [InlineData("/Items/00000000-0000-0000-0000-000000000001/stream", null)]
    [InlineData("/NebulaFtp/Download", "nebula")]
    [InlineData("/ClientLog/Document", "client-log")]
    [InlineData("/ClientLog/Document/Extra", "client-log")]
    [InlineData("/ClientLogger/Document", null)]
    [InlineData("/Backup/Create", "administration")]
    [InlineData("/System/Logs", "administration")]
    [InlineData("/Systematic/Logs", null)]
    [InlineData("/Videos/00000000000000000000000000000001/stream", null)]
    public void SelectiveRateLimitCategory_RequiresRouteBoundary(string path, string? expected)
    {
        Assert.Equal(expected, RateLimitMiddleware.GetSelectiveRateLimitCategory(path));
    }

    [Theory]
    [InlineData("GET", "/Audio/00000000-0000-0000-0000-000000000001/RemoteSearch/Lyrics", "search")]
    [InlineData("POST", "/Audio/00000000-0000-0000-0000-000000000001/RemoteSearch/Lyrics/provider-id", "search")]
    [InlineData("GET", "/Items/00000000-0000-0000-0000-000000000001/RemoteSearch/Subtitles/pt-BR", "search")]
    [InlineData("POST", "/Items/00000000-0000-0000-0000-000000000001/RemoteSearch/Subtitles/subtitle-id", "search")]
    [InlineData("GET", "/Providers/Lyrics/remote-lyric-id", "search")]
    [InlineData("GET", "/Providers/Lyrics/remote-lyric-id/", "search")]
    [InlineData("GET", "/Providers/Subtitles/Subtitles/remote-subtitle-id", "search")]
    [InlineData("GET", "/Providers/Lyrics/", null)]
    [InlineData("GET", "/Providers/Lyrics/id/extra", null)]
    [InlineData("GET", "/Providers/Lyrics/id//", null)]
    [InlineData("GET", "/Providers/Subtitles/Subtitles/id/extra", null)]
    [InlineData("POST", "/Providers/Lyrics/remote-lyric-id", null)]
    [InlineData("GET", "/Providers/LyricsFake/remote-lyric-id", null)]
    [InlineData("POST", "/Items/00000000-0000-0000-0000-000000000001/Refresh", "administration")]
    [InlineData("POST", "/Items/00000000-0000-0000-0000-000000000001", "administration")]
    [InlineData("POST", "/Audio/00000000-0000-0000-0000-000000000001/Lyrics", "administration")]
    [InlineData("DELETE", "/Audio/00000000-0000-0000-0000-000000000001/Lyrics", "administration")]
    [InlineData("POST", "/Videos/00000000-0000-0000-0000-000000000001/Subtitles", "administration")]
    [InlineData("GET", "/Audio/00000000-0000-0000-0000-000000000001/Lyrics", null)]
    [InlineData("GET", "/Videos/00000000-0000-0000-0000-000000000001/source/Subtitles/0/Stream", null)]
    [InlineData("POST", "/Audio/not-a-guid/RemoteSearch/Lyrics/provider-id", null)]
    [InlineData("POST", "/Items/00000000-0000-0000-0000-000000000001/Refresh/extra", null)]
    public void SelectiveRateLimitCategory_UsesMethodWithoutThrottlingPlayback(string method, string path, string? expected)
    {
        Assert.Equal(expected, RateLimitMiddleware.GetSelectiveRateLimitCategory(method, path));
    }

    [Fact]
    public async Task CatalogAudit_UsesAdministrativeQuotaAndReturnsRetryAfter()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);
        var userId = Guid.NewGuid();

        DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(InternalClaimTypes.UserId, userId.ToString("N"))],
                    "test"))
            };
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.161");
            context.Request.Path = "/Catalog/Audit";
            return context;
        }

        for (var request = 0; request < 20; request++)
        {
            var allowed = CreateContext();
            await middleware.Invoke(allowed);
            Assert.Equal(StatusCodes.Status200OK, allowed.Response.StatusCode);
        }

        var blocked = CreateContext();
        await middleware.Invoke(blocked);

        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.True(int.TryParse(blocked.Response.Headers.RetryAfter, out var retryAfter));
        Assert.InRange(retryAfter, 1, 10);
    }

    [Fact]
    public async Task AuthenticatedNebulaRequests_AreLimitedSeparatelyFromAnonymousTraffic()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 10; i++)
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity("test"))
            };
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.77");
            context.Request.Path = "/NebulaFtp/Download";
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        var blocked = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity("test"))
        };
        blocked.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.77");
        blocked.Request.Path = "/NebulaFtp/Download";
        await middleware.Invoke(blocked);

        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.True(blocked.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task RejectedRequests_EmitOnlyLowCardinalityCategoryMetric()
    {
        var measurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == RateLimitMiddleware.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) => measurements.Add((value, tags.ToArray())));
        listener.Start();

        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);
        var userId = Guid.NewGuid();

        for (var i = 0; i < 11; i++)
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(InternalClaimTypes.UserId, userId.ToString("N"))],
                    "test"))
            };
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.88");
            context.Request.Path = "/NebulaFtp/Download";
            await middleware.Invoke(context);
        }

        var measurement = Assert.Single(measurements);
        Assert.Equal(1, measurement.Value);
        var category = Assert.Single(measurement.Tags);
        Assert.Equal("category", category.Key);
        Assert.Equal("nebula", category.Value);
    }

    [Fact]
    public async Task AuthenticatedUsersBehindSameIp_HaveIndependentNebulaQuotas()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();

        DefaultHttpContext CreateContext(Guid userId)
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(InternalClaimTypes.UserId, userId.ToString("N"))],
                    "test"))
            };
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.82");
            context.Request.Path = "/NebulaFtp/Download";
            return context;
        }

        for (var i = 0; i < 10; i++)
        {
            var context = CreateContext(firstUserId);
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        var secondUserRequest = CreateContext(secondUserId);
        await middleware.Invoke(secondUserRequest);
        Assert.Equal(StatusCodes.Status200OK, secondUserRequest.Response.StatusCode);

        var firstUserBlocked = CreateContext(firstUserId);
        await middleware.Invoke(firstUserBlocked);
        Assert.Equal(StatusCodes.Status429TooManyRequests, firstUserBlocked.Response.StatusCode);
        Assert.True(firstUserBlocked.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task ClientLogUploads_AreLimitedPerAuthenticatedUserAndReturnRetryAfter()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();

        DefaultHttpContext CreateContext(Guid userId)
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(InternalClaimTypes.UserId, userId.ToString("N"))],
                    "test"))
            };
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.91");
            context.Request.Path = "/ClientLog/Document";
            context.Request.Method = "POST";
            return context;
        }

        for (var i = 0; i < 5; i++)
        {
            var allowed = CreateContext(firstUserId);
            await middleware.Invoke(allowed);
            Assert.Equal(StatusCodes.Status200OK, allowed.Response.StatusCode);
        }

        var blocked = CreateContext(firstUserId);
        await middleware.Invoke(blocked);
        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.True(int.TryParse(blocked.Response.Headers.RetryAfter, out var retryAfter));
        Assert.InRange(retryAfter, 1, 60);

        var independentUser = CreateContext(secondUserId);
        await middleware.Invoke(independentUser);
        Assert.Equal(StatusCodes.Status200OK, independentUser.Response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentNebulaRequests_DoNotExceedTheWindowQuota()
    {
        var releaseRequests = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var middleware = new RateLimitMiddleware(
            async context =>
            {
                await releaseRequests.Task;
                context.Response.StatusCode = StatusCodes.Status200OK;
            },
            NullLogger<RateLimitMiddleware>.Instance);
        var userId = Guid.NewGuid();
        var contexts = Enumerable.Range(0, 20).Select(_ =>
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(InternalClaimTypes.UserId, userId.ToString("N"))],
                    "test"))
            };
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.83");
            context.Request.Path = "/NebulaFtp/Download";
            return context;
        }).ToArray();

        var requests = contexts.Select(middleware.Invoke).ToArray();
        releaseRequests.SetResult();
        await Task.WhenAll(requests);

        Assert.Equal(10, contexts.Count(context => context.Response.StatusCode == StatusCodes.Status200OK));
        Assert.Equal(10, contexts.Count(context => context.Response.StatusCode == StatusCodes.Status429TooManyRequests));
        Assert.All(
            contexts.Where(context => context.Response.StatusCode == StatusCodes.Status429TooManyRequests),
            context => Assert.True(context.Response.Headers.ContainsKey("Retry-After")));
    }

    [Fact]
    public async Task MissingOrInvalidAuthenticatedUserId_FallsBackToSharedIpQuota()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        DefaultHttpContext CreateContext(string? userId)
        {
            var claims = userId is null ? Array.Empty<Claim>() : [new Claim(InternalClaimTypes.UserId, userId)];
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            };
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.84");
            context.Request.Path = "/NebulaFtp/Download";
            return context;
        }

        for (var i = 0; i < 10; i++)
        {
            var context = CreateContext(i % 2 == 0 ? null : "not-a-guid");
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        var blocked = CreateContext(Guid.Empty.ToString("N"));
        await middleware.Invoke(blocked);
        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedApiKeysBehindSameIp_HaveIndependentNebulaQuotas()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        DefaultHttpContext CreateContext(string token)
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(InternalClaimTypes.UserId, Guid.Empty.ToString("N")),
                        new Claim(InternalClaimTypes.IsApiKey, bool.TrueString),
                        new Claim(InternalClaimTypes.Token, token)
                    ],
                    "test"))
            };
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.85");
            context.Request.Path = "/NebulaFtp/Download";
            return context;
        }

        for (var i = 0; i < 10; i++)
        {
            var context = CreateContext("worker-a-key");
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        var secondWorker = CreateContext("worker-b-key");
        await middleware.Invoke(secondWorker);
        Assert.Equal(StatusCodes.Status200OK, secondWorker.Response.StatusCode);

        var firstWorkerBlocked = CreateContext("worker-a-key");
        await middleware.Invoke(firstWorkerBlocked);
        Assert.Equal(StatusCodes.Status429TooManyRequests, firstWorkerBlocked.Response.StatusCode);
    }

    [Fact]
    public async Task AnonymousRequests_AreRecordedAndBlockedAfterLimit()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 30; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.17");
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        var blocked = new DefaultHttpContext();
        blocked.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.17");
        await middleware.Invoke(blocked);

        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
    }

    [Fact]
    public async Task AnonymousRequests_BlockedResponse_IncludesRetryAfterHeader()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 30; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.20");
            await middleware.Invoke(context);
        }

        var blocked = new DefaultHttpContext();
        blocked.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.20");
        await middleware.Invoke(blocked);

        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.True(blocked.Response.Headers.ContainsKey("Retry-After"));
        var retryAfter = int.Parse(blocked.Response.Headers.RetryAfter.ToString(), System.Globalization.CultureInfo.InvariantCulture);
        // The anonymous window is 10 seconds; the header must be a positive value no larger
        // than the full window (it reports the remaining wait, never the whole window itself
        // unless every request landed in the same instant).
        Assert.InRange(retryAfter, 1, 10);
    }

    [Fact]
    public async Task LoginAttempts_BlockedResponse_IncludesRetryAfterHeader()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 10; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.21");
            context.Request.Path = "/Users/Authenticate";
            await middleware.Invoke(context);
        }

        var blocked = new DefaultHttpContext();
        blocked.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.21");
        blocked.Request.Path = "/Users/Authenticate";
        await middleware.Invoke(blocked);

        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.True(blocked.Response.Headers.ContainsKey("Retry-After"));
        var retryAfter = int.Parse(blocked.Response.Headers.RetryAfter.ToString(), System.Globalization.CultureInfo.InvariantCulture);
        // The login window is 15 minutes (900s); the header must be positive and never exceed it.
        Assert.InRange(retryAfter, 1, 900);
    }

    [Fact]
    public async Task ConcurrentLogins_AreCappedWhileInProgressAndSuccessfulAttemptsReleaseCapacity()
    {
        var releaseRequests = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var middleware = new RateLimitMiddleware(
            async context =>
            {
                await releaseRequests.Task;
                context.Response.StatusCode = StatusCodes.Status200OK;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.86");
            context.Request.Path = "/Users/Authenticate";
            return context;
        }

        var inProgress = Enumerable.Range(0, 10).Select(_ => CreateContext()).ToArray();
        var pending = inProgress.Select(middleware.Invoke).ToArray();
        var blocked = CreateContext();
        var blockedRequest = middleware.Invoke(blocked);
        releaseRequests.SetResult();
        await Task.WhenAll(pending.Append(blockedRequest));
        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.Equal("1", blocked.Response.Headers.RetryAfter.ToString());
        Assert.All(inProgress, context => Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode));

        var afterCompletion = CreateContext();
        await middleware.Invoke(afterCompletion);
        Assert.Equal(StatusCodes.Status200OK, afterCompletion.Response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentFailedLogins_RecordOnlyAdmittedFailures()
    {
        var releaseRequests = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var middleware = new RateLimitMiddleware(
            async context =>
            {
                await releaseRequests.Task;
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        var contexts = Enumerable.Range(0, 20).Select(_ =>
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.87");
            context.Request.Path = "/Users/Authenticate";
            return context;
        }).ToArray();
        var pending = contexts.Select(middleware.Invoke).ToArray();
        releaseRequests.SetResult();
        await Task.WhenAll(pending);

        Assert.Equal(10, contexts.Count(context => context.Response.StatusCode == StatusCodes.Status401Unauthorized));
        Assert.Equal(10, contexts.Count(context => context.Response.StatusCode == StatusCodes.Status429TooManyRequests));
        Assert.All(
            contexts.Where(context => context.Response.StatusCode == StatusCodes.Status429TooManyRequests),
            context => Assert.True(context.Response.Headers.ContainsKey("Retry-After")));
    }

    [Fact]
    public async Task InFlightLogin_ReservesRemainingFailureWindowCapacity()
    {
        var releaseRequest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdRequest = false;
        var middleware = new RateLimitMiddleware(
            async context =>
            {
                if (holdRequest)
                {
                    await releaseRequest.Task;
                    context.Response.StatusCode = StatusCodes.Status200OK;
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                }
            },
            NullLogger<RateLimitMiddleware>.Instance);

        DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.88");
            context.Request.Path = "/Users/Authenticate";
            return context;
        }

        for (var i = 0; i < 9; i++)
        {
            var failed = CreateContext();
            await middleware.Invoke(failed);
            Assert.Equal(StatusCodes.Status401Unauthorized, failed.Response.StatusCode);
        }

        holdRequest = true;
        var inProgress = CreateContext();
        var inProgressTask = middleware.Invoke(inProgress);
        var blocked = CreateContext();
        var blockedTask = middleware.Invoke(blocked);
        releaseRequest.SetResult();
        await Task.WhenAll(inProgressTask, blockedTask);

        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, inProgress.Response.StatusCode);

        var afterCompletion = CreateContext();
        await middleware.Invoke(afterCompletion);
        Assert.Equal(StatusCodes.Status200OK, afterCompletion.Response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentHeavyBackupRequests_AreRejectedWithoutBlockingPlayback()
    {
        var releaseBackup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var middleware = new RateLimitMiddleware(
            async context =>
            {
                if (context.Request.Path == "/Backup/Create")
                {
                    await releaseBackup.Task;
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        DefaultHttpContext CreateContext(string path, string method = "POST")
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.89");
            context.Request.Path = path;
            context.Request.Method = method;
            return context;
        }

        var backup = CreateContext("/Backup/Create");
        var pendingBackup = middleware.Invoke(backup);
        var competingBackup = CreateContext("/Backup/Create");
        var competingRequest = middleware.Invoke(competingBackup);
        var playback = CreateContext("/Videos/00000000000000000000000000000001/stream", "GET");
        await middleware.Invoke(playback);
        releaseBackup.SetResult();
        await Task.WhenAll(pendingBackup, competingRequest);

        Assert.Equal(StatusCodes.Status200OK, backup.Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, competingBackup.Response.StatusCode);
        Assert.Equal("1", competingBackup.Response.Headers.RetryAfter.ToString());
        Assert.Equal(StatusCodes.Status200OK, playback.Response.StatusCode);

        var afterCompletion = CreateContext("/Backup/Create");
        await middleware.Invoke(afterCompletion);
        Assert.Equal(StatusCodes.Status200OK, afterCompletion.Response.StatusCode);
    }

    [Fact]
    public async Task SupabaseRestoreAndFullSystemBackup_ShareHeavyOperationCapacity()
    {
        var releaseRestore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var middleware = new RateLimitMiddleware(
            async context =>
            {
                if (context.Request.Path == "/NebulaFtp/Supabase/Restore")
                {
                    await releaseRestore.Task;
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        DefaultHttpContext CreateContext(string path)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.90");
            context.Request.Path = path;
            context.Request.Method = "POST";
            return context;
        }

        var restore = CreateContext("/NebulaFtp/Supabase/Restore");
        var pendingRestore = middleware.Invoke(restore);
        var competingBackup = CreateContext("/Backup/Create");
        var competingRequest = middleware.Invoke(competingBackup);
        releaseRestore.SetResult();
        await Task.WhenAll(pendingRestore, competingRequest);

        Assert.Equal(StatusCodes.Status200OK, restore.Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, competingBackup.Response.StatusCode);
        Assert.Equal("1", competingBackup.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task HeavyOperationWithoutRemoteIp_StillUsesGlobalCapacity()
    {
        var releaseBackup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var middleware = new RateLimitMiddleware(
            async context =>
            {
                await releaseBackup.Task;
                context.Response.StatusCode = StatusCodes.Status200OK;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext();
            context.Request.Path = "/Backup/Create";
            context.Request.Method = "POST";
            return context;
        }

        var first = CreateContext();
        var pendingFirst = middleware.Invoke(first);
        var second = CreateContext();
        var pendingSecond = middleware.Invoke(second);
        releaseBackup.SetResult();
        await Task.WhenAll(pendingFirst, pendingSecond);

        Assert.Equal(StatusCodes.Status200OK, first.Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, second.Response.StatusCode);
    }

    [Fact]
    public async Task HeavyOperationException_ReleasesCapacity()
    {
        var shouldThrow = true;
        var middleware = new RateLimitMiddleware(
            context =>
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException("simulated backup failure");
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        DefaultHttpContext CreateContext()
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.91");
            context.Request.Path = "/Backup/Create";
            context.Request.Method = "POST";
            return context;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.Invoke(CreateContext()));
        shouldThrow = false;
        var next = CreateContext();
        await middleware.Invoke(next);
        Assert.Equal(StatusCodes.Status200OK, next.Response.StatusCode);
    }

    [Fact]
    public async Task CatalogScan_HasItsOwnCapacityAndDoesNotLimitReadOnlyRequests()
    {
        var releaseScan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var middleware = new RateLimitMiddleware(
            async context =>
            {
                if (context.Request.Method == "POST" && context.Request.Path == "/NebulaFtp/Actions/ScanNovelas")
                {
                    await releaseScan.Task;
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        DefaultHttpContext CreateContext(string path, string method)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.92");
            context.Request.Path = path;
            context.Request.Method = method;
            return context;
        }

        var firstScan = CreateContext("/NebulaFtp/Actions/ScanNovelas", "POST");
        var pendingFirstScan = middleware.Invoke(firstScan);
        var competingScan = CreateContext("/NebulaFtp/Actions/ScanNovelas", "POST");
        var pendingCompetingScan = middleware.Invoke(competingScan);
        var readOnly = CreateContext("/NebulaFtp/Actions/ScanNovelas", "GET");
        await middleware.Invoke(readOnly);
        releaseScan.SetResult();
        await Task.WhenAll(pendingFirstScan, pendingCompetingScan);

        Assert.Equal(StatusCodes.Status200OK, firstScan.Response.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, competingScan.Response.StatusCode);
        Assert.Equal(StatusCodes.Status200OK, readOnly.Response.StatusCode);
    }

    [Fact]
    public async Task AnonymousStaticAssets_AreNotCountedAgainstRequestLimit()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 40; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.18");
            context.Request.Path = "/assets/index.js";
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task AnonymousPublicBootstrapRequests_AreNotCountedAgainstRequestLimit()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 40; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.19");
            context.Request.Path = "/System/Info/Public";
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }

    [Fact]
    public async Task AnonymousLoopbackRequests_AreNotRateLimited()
    {
        var middleware = new RateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<RateLimitMiddleware>.Instance);

        for (var i = 0; i < 40; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Request.Path = "/Users/test-id/Views";
            await middleware.Invoke(context);
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }
    }
}
