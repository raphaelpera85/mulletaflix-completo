using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Jellyfin.Database.Implementations.Migrations;
using MulletaFlix.Database.Implementations.Migrations;
using Xunit;
using System;

namespace MulletaFlix.Server.Implementations.Tests.EfMigrations;

public class EfMigrationTests
{
    private sealed class ExposedFixBaseItemRedundantRelations : FixBaseItemRedundantRelations
    {
        public void Apply(MigrationBuilder migrationBuilder)
        {
            base.Up(migrationBuilder);
        }
    }

    private sealed class ExposedAddRemoteImageProvenance : AddRemoteImageProvenance
    {
        public void ApplyUp(MigrationBuilder migrationBuilder)
        {
            base.Up(migrationBuilder);
        }

        public void ApplyDown(MigrationBuilder migrationBuilder)
        {
            base.Down(migrationBuilder);
        }
    }

    [Fact]
    public void CheckForUnappliedMigrations_MySql()
    {
        var dbDesignContext = new MySqlDesignTimeMulletaFlixDbFactory();
        var context = dbDesignContext.CreateDbContext([]);
        Assert.False(context.Database.HasPendingModelChanges(), "There are unapplied changes to the EFCore model for MySQL. Run: dotnet ef migrations add --context MulletaFlixDbContext --project src/Jellyfin.Database/Jellyfin.Database.Implementations");
    }

    [Fact]
    public void FixBaseItemRedundantRelations_DropsDuplicateFullTextIndexBeforeRecreatingIt()
    {
        var migration = new ExposedFixBaseItemRedundantRelations();
        var builder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        migration.Apply(builder);

        var operations = builder.Operations;
        var dropIndex = Assert.IsType<SqlOperation>(operations[1]);
        Assert.Contains("DROP INDEX IF EXISTS IX_BaseItems_FullTextSearch ON BaseItems;", dropIndex.Sql, StringComparison.Ordinal);

        var createIndex = Assert.IsType<CreateIndexOperation>(operations[2]);
        Assert.Equal("IX_BaseItems_FullTextSearch", createIndex.Name);
    }

    [Fact]
    public void AddRemoteImageProvenance_AddsNullableColumnsAndRevertsOnlyThoseColumns()
    {
        var migration = new ExposedAddRemoteImageProvenance();
        var upBuilder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");

        migration.ApplyUp(upBuilder);

        Assert.Collection(
            upBuilder.Operations,
            operation => AssertImageProvenanceColumn(operation, "Language", "varchar(32)"),
            operation => AssertImageProvenanceColumn(operation, "ProviderName", "varchar(255)"));

        var downBuilder = new MigrationBuilder("Pomelo.EntityFrameworkCore.MySql");
        migration.ApplyDown(downBuilder);

        Assert.Collection(
            downBuilder.Operations,
            operation => AssertImageProvenanceDrop(operation, "Language"),
            operation => AssertImageProvenanceDrop(operation, "ProviderName"));
    }

    private static void AssertImageProvenanceColumn(MigrationOperation operation, string name, string type)
    {
        var column = Assert.IsType<AddColumnOperation>(operation);
        Assert.Equal("BaseItemImageInfos", column.Table);
        Assert.Equal(name, column.Name);
        Assert.Equal(type, column.ColumnType);
        Assert.True(column.IsNullable);
    }

    private static void AssertImageProvenanceDrop(MigrationOperation operation, string name)
    {
        var column = Assert.IsType<DropColumnOperation>(operation);
        Assert.Equal("BaseItemImageInfos", column.Table);
        Assert.Equal(name, column.Name);
    }
}
