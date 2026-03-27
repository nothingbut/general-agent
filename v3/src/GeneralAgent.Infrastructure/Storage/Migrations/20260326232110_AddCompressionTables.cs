using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeneralAgent.Infrastructure.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddCompressionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "compression_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AutoCompressionEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AutoCompressionThreshold = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultStrategy = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    StrategyOptionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compression_configs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "compression_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StrategyUsed = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    OriginalMessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CompressedMessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    CompressedTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    CompressionRatio = table.Column<double>(type: "REAL", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    CompressedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compression_history", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_compression_configs_CreatedAt",
                table: "compression_configs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_compression_configs_SessionId",
                table: "compression_configs",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compression_history_CompressedAt",
                table: "compression_history",
                column: "CompressedAt");

            migrationBuilder.CreateIndex(
                name: "IX_compression_history_SessionId",
                table: "compression_history",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_compression_history_StrategyUsed",
                table: "compression_history",
                column: "StrategyUsed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "compression_configs");

            migrationBuilder.DropTable(
                name: "compression_history");
        }
    }
}
