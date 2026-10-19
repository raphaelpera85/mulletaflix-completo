using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public sealed class NebulaPeriodicLoopTests
{
    [Fact]
    public async Task RunImmediatelyThenPeriodicallyAsync_RunsWorkBeforeWaiting()
    {
        var events = new List<string>();

        await Jellyfin.Server.Implementations.Nebula.NebulaPeriodicLoop.RunImmediatelyThenPeriodicallyAsync(
            _ =>
            {
                events.Add("work");
                return Task.CompletedTask;
            },
            _ =>
            {
                events.Add("wait");
                return ValueTask.FromResult(false);
            },
            CancellationToken.None);

        Assert.Equal(new[] { "work", "wait" }, events);
    }

    [Fact]
    public async Task RunImmediatelyThenPeriodicallyAsync_RunsOncePerIntervalTick()
    {
        var workCount = 0;
        var tickCount = 0;

        await Jellyfin.Server.Implementations.Nebula.NebulaPeriodicLoop.RunImmediatelyThenPeriodicallyAsync(
            _ =>
            {
                workCount++;
                return Task.CompletedTask;
            },
            _ => ValueTask.FromResult(++tickCount < 3),
            CancellationToken.None);

        Assert.Equal(3, workCount);
        Assert.Equal(3, tickCount);
    }
}
