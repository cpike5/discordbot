using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddLlmUsageRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "DmAssistantInteractionLogs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "AssistantInteractionLogs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LlmUsageRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CachedTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CacheWriteTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LlmCalls = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ToolCalls = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CostUsd = table.Column<decimal>(type: "decimal(18,8)", nullable: false, defaultValue: 0m),
                    CostSource = table.Column<int>(type: "INTEGER", nullable: false),
                    LatencyMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    InteractionLogId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmUsageRecords_GuildId_Timestamp",
                table: "LlmUsageRecords",
                columns: new[] { "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmUsageRecords_Mode_Timestamp",
                table: "LlmUsageRecords",
                columns: new[] { "Mode", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmUsageRecords_Model_Timestamp",
                table: "LlmUsageRecords",
                columns: new[] { "Model", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmUsageRecords_UserId_Timestamp",
                table: "LlmUsageRecords",
                columns: new[] { "UserId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlmUsageRecords");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "DmAssistantInteractionLogs");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "AssistantInteractionLogs");
        }
    }
}
