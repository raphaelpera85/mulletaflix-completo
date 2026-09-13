using System;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Database.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MulletaFlix.Server.Health;

/// <summary>
/// Verifies the database through the registered context factory.
/// </summary>
public sealed class MulletaFlixDatabaseHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<MulletaFlixDbContext> _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MulletaFlixDatabaseHealthCheck"/> class.
    /// </summary>
    /// <param name="dbContextFactory">Factory for application database contexts.</param>
    public MulletaFlixDatabaseHealthCheck(IDbContextFactory<MulletaFlixDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await _dbContextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            var canConnect = await dbContext.Database
                .CanConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            return canConnect
                ? HealthCheckResult.Healthy("Banco de dados respondeu ao health check.")
                : HealthCheckResult.Unhealthy("Banco de dados não está acessível.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Falha ao verificar o banco de dados.", ex);
        }
    }
}
