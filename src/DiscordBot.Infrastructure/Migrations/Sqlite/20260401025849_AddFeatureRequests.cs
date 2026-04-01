using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddFeatureRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureRequestRejections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureRequestRejections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    SubmittedByUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    GatheredRequirements = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ConsolidatedSummary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewedByUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    DocBranchName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DocPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    DocGenError = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeatureRequests_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureRequestRejections_GuildId_UserId_CreatedAt",
                table: "FeatureRequestRejections",
                columns: new[] { "GuildId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureRequests_GuildId_Status_CreatedAt",
                table: "FeatureRequests",
                columns: new[] { "GuildId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureRequestRejections");

            migrationBuilder.DropTable(
                name: "FeatureRequests");
        }
    }
}
