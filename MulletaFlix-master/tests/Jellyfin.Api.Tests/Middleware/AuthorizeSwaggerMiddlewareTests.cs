using System.Security.Claims;
using System.Threading.Tasks;
using MulletaFlix.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Jellyfin.Api.Tests.Middleware;

public sealed class AuthorizeSwaggerMiddlewareTests
{
    [Theory]
    [InlineData("/api-docs")]
    [InlineData("/api-docs/swagger")]
    [InlineData("/api-docs/redoc")]
    [InlineData("/api-docs/openapi.json")]
    public async Task AuthorizeSwagger_UnauthorizedRequest_ReturnsForbidden(string path)
    {
        var middleware = new AuthorizeSwaggerMiddleware(_ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // Unauthenticated

        await middleware.Invoke(httpContext);

        // Unauthenticated users should get 401
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/items")]
    [InlineData("/playback")]
    public async Task AuthorizeSwagger_NonSwaggerPath_AllowsAccess(string path)
    {
        var callCount = 0;
        RequestDelegate nextMiddleware = _ =>
        {
            callCount++;
            return Task.CompletedTask;
        };
        var middleware = new AuthorizeSwaggerMiddleware(nextMiddleware);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;

        await middleware.Invoke(httpContext);

        // Non-Swagger paths should pass through to next middleware
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task AuthorizeSwagger_SwaggerPathWithValidClaims_AllowsAccess()
    {
        var callCount = 0;
        RequestDelegate nextMiddleware = _ =>
        {
            callCount++;
            return Task.CompletedTask;
        };
        var middleware = new AuthorizeSwaggerMiddleware(nextMiddleware);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api-docs/swagger";

        // Create authenticated user with admin claim
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Role, "Administrator")
            },
            "test");
        httpContext.User = new ClaimsPrincipal(identity);

        await middleware.Invoke(httpContext);

        // Admin user should get access
        Assert.Equal(1, callCount);
    }
}
