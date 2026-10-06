using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Http;

namespace MulletaFlix.Api.Middleware;

/// <summary>
/// Middleware that requires authorization for Swagger/OpenAPI endpoints.
/// Prevents unauthorized users from viewing the API documentation.
/// </summary>
public class AuthorizeSwaggerMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly string[] SwaggerPaths =
    {
        "/api-docs",
        "/api-docs/swagger",
        "/api-docs/redoc"
    };

    public AuthorizeSwaggerMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Check if the request is for a Swagger endpoint
        if (IsSwaggerPath(path))
        {
            // Allow access only to authenticated users with LocalAccessOrRequiresElevation
            // If user is not authenticated or doesn't have elevation, return 401/403
            if (!context.User.Identity?.IsAuthenticated ?? true)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            // Verify the user has the required policy
            if (!context.User.HasClaim(claim =>
                claim.Type == "IsAdmin" || claim.Type == System.Security.Claims.ClaimTypes.Role))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await _next(context);
    }

    private static bool IsSwaggerPath(string path)
    {
        foreach (var swaggerPath in SwaggerPaths)
        {
            if (path.StartsWith(swaggerPath, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
