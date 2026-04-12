using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GeneralAgent.Infrastructure.Storage.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractionRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtractionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    SkillName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SkillNamespace = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    Occurrences = table.Column<int>(type: "INTEGER", nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionRecords_Action",
                table: "ExtractionRecords",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionRecords_SessionId",
                table: "ExtractionRecords",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionRecords_Skill",
                table: "ExtractionRecords",
                columns: new[] { "SkillNamespace", "SkillName" });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionRecords_Timestamp",
                table: "ExtractionRecords",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtractionRecords");
        }
    }
}
