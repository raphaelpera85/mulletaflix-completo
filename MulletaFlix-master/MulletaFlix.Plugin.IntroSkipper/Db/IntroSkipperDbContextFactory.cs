// SPDX-FileCopyrightText: 2024-2026 rlauuzo
// SPDX-FileCopyrightText: 2024-2026 AbandonedCart
// SPDX-FileCopyrightText: 2024-2026 Kilian von Pflugk
// SPDX-License-Identifier: GPL-3.0-only

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql;

namespace IntroSkipper.Db;

/// <summary>
/// IntroSkipperDbContext factory.
/// </summary>
public class IntroSkipperDbContextFactory : IDesignTimeDbContextFactory<IntroSkipperDbContext>
{
    /// <inheritdoc/>
    public IntroSkipperDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IntroSkipperDbContext>();
        optionsBuilder.UseMySql(
            "Server=127.0.0.1;Port=3306;User ID=root;Password=;Database=mulletaflix;",
            new MariaDbServerVersion(new Version(11, 4, 2)));

        return new IntroSkipperDbContext(optionsBuilder.Options);
    }
}
