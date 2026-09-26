using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Database.Implementations;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Server.Implementations.Billing;
using MulletaFlix.Server.ServerSetupApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Server.Migrations.Routines;

/// <summary>
/// Creates the per-domain (Movies/Series/Channels/Books) MariaDB schemas and migrates
/// legacy <c>BaseItems</c> rows into them.
/// </summary>
/// <remarks>
/// This used to run unconditionally on every server boot from
/// <c>MulletaFlixMigrationService.MigrateStepAsync</c>, issuing 12 information_schema probes
/// and 4 <c>AnyAsync</c> checks per startup regardless of whether the work had already
/// happened. Wrapping it in a regular code migration means the normal EF migration-history
/// bookkeeping (the <c>__EFMigrationsHistory</c> row inserted by <c>InternalCodeMigration</c>
/// after a successful run) marks it as applied, so subsequent boots skip it entirely.
/// </remarks>
[MulletaFlixMigration("2026-09-26T12:00:00", nameof(InitializeDomainSchemasAndMigrateLegacyMedia))]
public class InitializeDomainSchemasAndMigrateLegacyMedia : IAsyncMigrationRoutine
{
    private readonly IStartupLogger<InitializeDomainSchemasAndMigrateLegacyMedia> _logger;
    private readonly IDbContextFactory<MulletaFlixDbContext> _legacyDbContextFactory;
    private readonly IDbContextFactory<MoviesDbContext> _moviesDbContextFactory;
    private readonly IDbContextFactory<SeriesDbContext> _seriesDbContextFactory;
    private readonly IDbContextFactory<ChannelsDbContext> _channelsDbContextFactory;
    private readonly IDbContextFactory<BooksDbContext> _booksDbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitializeDomainSchemasAndMigrateLegacyMedia"/> class.
    /// </summary>
    /// <param name="logger">The startup logger.</param>
    /// <param name="legacyDbContextFactory">Provides access to the legacy MulletaFlix database.</param>
    /// <param name="moviesDbContextFactory">Provides access to the movies domain database.</param>
    /// <param name="seriesDbContextFactory">Provides access to the series domain database.</param>
    /// <param name="channelsDbContextFactory">Provides access to the channels domain database.</param>
    /// <param name="booksDbContextFactory">Provides access to the books domain database.</param>
    public InitializeDomainSchemasAndMigrateLegacyMedia(
        IStartupLogger<InitializeDomainSchemasAndMigrateLegacyMedia> logger,
        IDbContextFactory<MulletaFlixDbContext> legacyDbContextFactory,
        IDbContextFactory<MoviesDbContext> moviesDbContextFactory,
        IDbContextFactory<SeriesDbContext> seriesDbContextFactory,
        IDbContextFactory<ChannelsDbContext> channelsDbContextFactory,
        IDbContextFactory<BooksDbContext> booksDbContextFactory)
    {
        _logger = logger;
        _legacyDbContextFactory = legacyDbContextFactory;
        _moviesDbContextFactory = moviesDbContextFactory;
        _seriesDbContextFactory = seriesDbContextFactory;
        _channelsDbContextFactory = channelsDbContextFactory;
        _booksDbContextFactory = booksDbContextFactory;
    }

    /// <inheritdoc/>
    public async Task PerformAsync(CancellationToken cancellationToken)
    {
        await InitializeDomainSchemasAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Migrating legacy media data to domain schemas.");
        await DomainDataMigrator.MigrateAsync(
            _legacyDbContextFactory,
            _moviesDbContextFactory,
            _seriesDbContextFactory,
            _channelsDbContextFactory,
            _booksDbContextFactory,
            _logger,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task InitializeDomainSchemasAsync(CancellationToken cancellationToken)
    {
        var moviesCtx = await _moviesDbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (moviesCtx.ConfigureAwait(false))
        {
            await DomainSchemaInitializer.EnsureDomainTablesAsync(moviesCtx, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("Movies schema initialized.");
        }

        var seriesCtx = await _seriesDbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (seriesCtx.ConfigureAwait(false))
        {
            await DomainSchemaInitializer.EnsureDomainTablesAsync(seriesCtx, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("Series schema initialized.");
        }

        var channelsCtx = await _channelsDbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (channelsCtx.ConfigureAwait(false))
        {
            await DomainSchemaInitializer.EnsureDomainTablesAsync(channelsCtx, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("Channels schema initialized.");
        }

        var booksCtx = await _booksDbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await using (booksCtx.ConfigureAwait(false))
        {
            await DomainSchemaInitializer.EnsureDomainTablesAsync(booksCtx, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogInformation("Books schema initialized.");
        }
    }
}
