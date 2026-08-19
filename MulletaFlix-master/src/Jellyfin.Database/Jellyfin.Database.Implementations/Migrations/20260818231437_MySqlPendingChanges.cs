using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class MySqlPendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BaseItems_FullTextSearch",
                table: "BaseItems");

            migrationBuilder.CreateTable(
                name: "MidiaStorageOnlineMediaMetadata",
                columns: table => new
                {
                    RelativePath = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Year = table.Column<int>(type: "int", nullable: true),
                    SeasonNumber = table.Column<int>(type: "int", nullable: true),
                    EpisodeNumber = table.Column<int>(type: "int", nullable: true),
                    Mode = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceUrl = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalFileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecognizedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MidiaStorageOnlineMediaMetadata", x => x.RelativePath);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_FullTextSearch",
                table: "BaseItems",
                columns: new[] { "CleanName", "OriginalTitle" });

            migrationBuilder.CreateIndex(
                name: "IX_MidiaStorageOnlineMediaMetadata_SourceId",
                table: "MidiaStorageOnlineMediaMetadata",
                column: "SourceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MidiaStorageOnlineMediaMetadata");

            migrationBuilder.DropIndex(
                name: "IX_BaseItems_FullTextSearch",
                table: "BaseItems");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItems_FullTextSearch",
                table: "BaseItems",
                columns: new[] { "CleanName", "OriginalTitle" })
                .Annotation("MySql:FullTextIndex", true);
        }
    }
}
