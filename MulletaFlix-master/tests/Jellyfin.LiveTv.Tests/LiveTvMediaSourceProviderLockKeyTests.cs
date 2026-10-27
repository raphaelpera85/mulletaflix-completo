using System;
using System.Collections.Generic;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MulletaFlix.LiveTv.Tests;

public sealed class LiveTvMediaSourceProviderLockKeyTests
{
    private readonly LiveTvMediaSourceProvider _provider = new(
        NullLogger<LiveTvMediaSourceProvider>.Instance,
        Mock.Of<IServerApplicationHost>(),
        Mock.Of<IRecordingsManager>(),
        Mock.Of<IMediaSourceManager>(),
        Mock.Of<ILibraryManager>(),
        Array.Empty<ILiveTvService>());

    [Theory]
    [InlineData("LiveTvChannel_0123456789abcdef0123456789abcdef_stream-id", "stream-id")]
    [InlineData("LiveTvChannel_0123456789abcdef0123456789abcdef_stream_id", "stream_id")]
    [InlineData("LiveTvChannel_0123456789abcdef0123456789abcdef_", "LiveTvChannel_0123456789abcdef0123456789abcdef_")]
    public void GetOpenMediaSourceLockKey_UsesSourceIdAndPreservesTokenFallback(string token, string expected)
    {
        Assert.Equal(expected, _provider.GetOpenMediaSourceLockKey(token));
    }
}
