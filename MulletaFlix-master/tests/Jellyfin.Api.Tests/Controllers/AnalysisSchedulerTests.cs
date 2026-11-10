using System;
using System.Threading;
using System.Threading.Tasks;
using IntroSkipper.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public sealed class AnalysisSchedulerTests
{
    [Fact]
    public async Task StopAsync_WakesIdleWorker()
    {
        using var scheduler = new AnalysisScheduler(
            analyzer: null!,
            seasonResolver: null!,
            eraser: null!,
            timeProvider: TimeProvider.System,
            logger: NullLogger<AnalysisScheduler>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await scheduler.StartAsync(CancellationToken.None);
        await scheduler.StopAsync(timeout.Token);
    }
}
