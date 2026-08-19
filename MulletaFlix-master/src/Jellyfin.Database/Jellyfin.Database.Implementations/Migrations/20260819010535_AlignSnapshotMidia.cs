using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AlignSnapshotMidia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_BaseItems_FullTextSearch ON BaseItems;");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_FullTextSearch",
                table: "BaseItems",
                columns: new[] { "CleanName", "OriginalTitle" })
                .Annotation("MySql:FullTextIndex", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS IX_BaseItems_FullTextSearch ON BaseItems;");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_FullTextSearch",
                table: "BaseItems",
                columns: new[] { "CleanName", "OriginalTitle" });
        }
    }
}
