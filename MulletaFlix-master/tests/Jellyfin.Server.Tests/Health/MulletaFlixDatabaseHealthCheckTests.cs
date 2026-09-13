using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Locking;
using MulletaFlix.Server.Health;
using Xunit;

namespace MulletaFlix.Server.Tests.Health;

public sealed class MulletaFlixDatabaseHealthCheckTests
{
    [Fact]
    public async Task DatabaseIsHealthyWhenFactoryCanConnect()
    {
        var options = new DbContextOptionsBuilder<MulletaFlixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var provider = new Mock<IMulletaFlixDatabaseProvider>();
        var locking = new Mock<IEntityFrameworkCoreLockingBehavior>();
        var context = new MulletaFlixDbContext(
            options,
            NullLogger<MulletaFlixDbContext>.Instance,
            provider.Object,
            locking.Object);
        IDbContextFactory<MulletaFlixDbContext> factory = new TestDbContextFactory(context);

        var result = await new MulletaFlixDatabaseHealthCheck(factory)
            .CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task DatabaseIsUnhealthyWhenFactoryFails()
    {
        IDbContextFactory<MulletaFlixDbContext> factory = new FailingDbContextFactory();

        var result = await new MulletaFlixDatabaseHealthCheck(factory)
            .CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.NotNull(result.Exception);
    }

    private sealed class TestDbContextFactory : IDbContextFactory<MulletaFlixDbContext>
    {
        private readonly MulletaFlixDbContext _context;

        public TestDbContextFactory(MulletaFlixDbContext context)
        {
            _context = context;
        }

        public MulletaFlixDbContext CreateDbContext() => _context;

        public ValueTask<MulletaFlixDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => new(_context);
    }

    private sealed class FailingDbContextFactory : IDbContextFactory<MulletaFlixDbContext>
    {
        public MulletaFlixDbContext CreateDbContext() => throw new InvalidOperationException("database unavailable");

        public ValueTask<MulletaFlixDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("database unavailable");
    }
}
