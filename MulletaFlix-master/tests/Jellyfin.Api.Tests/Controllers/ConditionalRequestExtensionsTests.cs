using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using MulletaFlix.Api.Extensions;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class ConditionalRequestExtensionsTests
{
    [Theory]
    [InlineData("\"current\"", true)]
    [InlineData("W/\"current\"", true)]
    [InlineData("\"stale\", W/\"current\"", true)]
    [InlineData("*", true)]
    [InlineData("\"stale\"", false)]
    [InlineData("not-an-etag", false)]
    public void IfNoneMatchMatches_UsesWeakComparisonForTheTagList(string header, bool expected)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[HeaderNames.IfNoneMatch] = header;

        Assert.Equal(expected, request.IfNoneMatchMatches("\"current\""));
    }

    [Fact]
    public void IfModifiedSinceMatches_IgnoresDateWhenIfNoneMatchIsPresent()
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[HeaderNames.IfNoneMatch] = "\"stale\"";
        request.Headers[HeaderNames.IfModifiedSince] = DateTime.UtcNow.AddDays(1).ToString("R");

        Assert.False(request.IfModifiedSinceMatches(DateTime.UtcNow));
    }
}
