using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace MulletaFlix.Api.Extensions;

/// <summary>
/// Helpers for evaluating HTTP conditional requests.
/// </summary>
internal static class ConditionalRequestExtensions
{
    /// <summary>
    /// Checks whether an If-None-Match field matches the selected entity tag using weak comparison.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="entityTag">The selected entity tag, including quotes.</param>
    /// <returns><see langword="true"/> when the field matches the tag or contains a wildcard.</returns>
    public static bool IfNoneMatchMatches(this HttpRequest request, string entityTag)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(entityTag);

        var values = request.Headers[HeaderNames.IfNoneMatch].ToArray();
        if (values.Length == 0 || !EntityTagHeaderValue.TryParseList(values, out var entityTags))
        {
            return false;
        }

        foreach (var candidate in entityTags)
        {
            if (candidate.Equals(EntityTagHeaderValue.Any)
                || string.Equals(candidate.Tag.Value, entityTag, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks If-Modified-Since only when If-None-Match is absent.
    /// </summary>
    /// <param name="request">The HTTP request.</param>
    /// <param name="lastModified">The selected representation's modification time.</param>
    /// <returns><see langword="true"/> when the date condition matches.</returns>
    public static bool IfModifiedSinceMatches(this HttpRequest request, DateTime lastModified)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !request.Headers.ContainsKey(HeaderNames.IfNoneMatch)
            && DateTime.TryParse(request.Headers[HeaderNames.IfModifiedSince], out var ifModifiedSince)
            && lastModified <= ifModifiedSince;
    }
}
