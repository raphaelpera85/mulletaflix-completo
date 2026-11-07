using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace MulletaFlix.Api.Helpers;

internal static class ConditionalRequestHelper
{
    public static bool MatchesIfNoneMatch(HttpRequest request, string? entityTag)
    {
        var requestTags = request.GetTypedHeaders().IfNoneMatch;
        if (requestTags.Count == 0)
        {
            return false;
        }

        if (requestTags.Contains(EntityTagHeaderValue.Any))
        {
            return true;
        }

        return !string.IsNullOrEmpty(entityTag)
            && EntityTagHeaderValue.TryParse(entityTag, out var currentTag)
            && requestTags.Any(requestTag => requestTag.Compare(currentTag, useStrongComparison: false));
    }
}
