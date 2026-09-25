using System;
using MediaBrowser.Controller.Entities;
using MulletaFlix.Api.Helpers;
using Xunit;

namespace MulletaFlix.Api.Tests.Helpers;

public class TransientMediaItemRegistryTests
{
    [Fact]
    public void Register_MakesPathResolvedItemAvailableForPlaybackById()
    {
        var registry = new TransientMediaItemRegistry();
        var item = new Video
        {
            Id = Guid.NewGuid(),
            Path = @"C:\Program Files\MulletaFlix\Server\media\mulletaflix_intro.mp4"
        };

        registry.Register(item);

        Assert.True(registry.TryGet(item.Id, out var resolved));
        Assert.Same(item, resolved);
    }

    [Fact]
    public void Register_IgnoresItemsWithoutStableIdentityOrPath()
    {
        var registry = new TransientMediaItemRegistry();
        var item = new Video
        {
            Id = Guid.Empty,
            Path = @"C:\Program Files\MulletaFlix\Server\media\mulletaflix_intro.mp4"
        };

        registry.Register(item);

        Assert.False(registry.TryGet(item.Id, out _));
    }
}
