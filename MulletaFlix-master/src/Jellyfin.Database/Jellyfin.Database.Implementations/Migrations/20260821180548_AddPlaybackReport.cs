using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Database.Implementations.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaybackReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaybackReport",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ItemName = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ItemType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SeriesName = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SeasonNumber = table.Column<int>(type: "int", nullable: true),
                    EpisodeNumber = table.Column<int>(type: "int", nullable: true),
                    Artist = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Album = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviceId = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeviceName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClientName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlaySessionId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SessionId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartTimeUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndTimeUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DurationSeconds = table.Column<long>(type: "bigint", nullable: true),
                    StartPositionTicks = table.Column<long>(type: "bigint", nullable: true),
                    EndPositionTicks = table.Column<long>(type: "bigint", nullable: true),
                    ItemRuntimeTicks = table.Column<long>(type: "bigint", nullable: true),
                    CompletionPercentage = table.Column<double>(type: "double", nullable: true),
                    PlayedToCompletion = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    WasTranscoded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    VideoCodec = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AudioCodec = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Container = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Bitrate = table.Column<long>(type: "bigint", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    Protocol = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlayMethod = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RemoteEndPoint = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsLocal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LibraryId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    LibraryName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DateCreated = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LogSeverity = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<uint>(type: "int unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackReport", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackReport_DateCreated",
                table: "PlaybackReport",
                column: "DateCreated");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackReport_DeviceId",
                table: "PlaybackReport",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackReport_DeviceId_DateCreated",
                table: "PlaybackReport",
                columns: new[] { "DeviceId", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackReport_ItemId",
                table: "PlaybackReport",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackReport_ItemId_DateCreated",
                table: "PlaybackReport",
                columns: new[] { "ItemId", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackReport_PlaySessionId",
                table: "PlaybackReport",
                column: "PlaySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackReport_UserId",
                table: "PlaybackReport",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackReport_UserId_DateCreated",
                table: "PlaybackReport",
                columns: new[] { "UserId", "DateCreated" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlaybackReport");
        }
    }
}
