using MulletaFlix.Server.Migrations;
using Xunit;

namespace MulletaFlix.Server.Tests.Migrations;

public class MigrationStartupSelectionTests
{
    [Fact]
    public void IsMigrationNotApplied_ReturnsTrue_WhenOnlyDifferentMigrationIdsAreApplied()
    {
        Assert.True(MulletaFlixMigrationService.IsMigrationNotApplied("CodeMigration_1", ["EfMigration_1", "EfMigration_2"]));
    }

    [Fact]
    public void IsMigrationNotApplied_ReturnsFalse_WhenMigrationIdIsAlreadyApplied()
    {
        Assert.False(MulletaFlixMigrationService.IsMigrationNotApplied("CodeMigration_1", ["EfMigration_1", "CodeMigration_1"]));
    }

    [Fact]
    public void IsMigrationNotApplied_ReturnsTrue_WhenHistoryIsEmpty()
    {
        Assert.True(MulletaFlixMigrationService.IsMigrationNotApplied("CodeMigration_1", []));
    }
}
