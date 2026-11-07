using MulletaFlix.Api.Controllers;
using Xunit;
using Microsoft.AspNetCore.Http;
using MulletaFlix.Api.Helpers;

namespace MulletaFlix.Api.Tests.Controllers;

public static class ImageControllerTests
{
    [Theory]
    [InlineData("image/apng", ".apng")]
    [InlineData("image/avif", ".avif")]
    [InlineData("image/bmp", ".bmp")]
    [InlineData("image/gif", ".gif")]
    [InlineData("image/x-icon", ".ico")]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/png", ".png")]
    [InlineData("image/png; charset=utf-8", ".png")]
    [InlineData("image/svg+xml", ".svg")]
    [InlineData("image/tiff", ".tiff")]
    [InlineData("image/webp", ".webp")]
    public static void TryGetImageExtensionFromContentType_Valid_True(string contentType, string extension)
    {
        Assert.True(ImageController.TryGetImageExtensionFromContentType(contentType, out var ex));
        Assert.Equal(extension, ex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("text/html")]
    public static void TryGetImageExtensionFromContentType_InValid_False(string? contentType)
    {
        Assert.False(ImageController.TryGetImageExtensionFromContentType(contentType, out var ex));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("\"current\"", "\"current\"", true)]
    [InlineData("W/\"current\"", "\"current\"", true)]
    [InlineData("\"old\", W/\"current\"", "\"current\"", true)]
    [InlineData("\"old\"", "\"current\"", false)]
    [InlineData("*", null, true)]
    public static void MatchesIfNoneMatch_UsesWeakComparisonAndSupportsListsAndWildcard(string requestHeader, string? entityTag, bool expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers.IfNoneMatch = requestHeader;

        Assert.Equal(expected, ConditionalRequestHelper.MatchesIfNoneMatch(request, entityTag));
    }
}

