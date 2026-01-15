using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantGuildSettings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AllowedChannelIds = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    RateLimitOverride = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantGuildSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_AssistantGuildSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssistantInteractionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Response = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CachedTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CacheCreationTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CacheHit = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ToolCalls = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LatencyMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,8)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantInteractionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantInteractionLogs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssistantInteractionLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssistantUsageMetrics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalQuestions = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalInputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalOutputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalCachedTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalCacheWriteTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalCacheHits = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalCacheMisses = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalToolCalls = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,8)", nullable: false, defaultValue: 0m),
                    FailedRequests = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AverageLatencyMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantUsageMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantUsageMetrics_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantInteractionLogs_GuildId_Timestamp",
                table: "AssistantInteractionLogs",
                columns: new[] { "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantInteractionLogs_Timestamp",
                table: "AssistantInteractionLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantInteractionLogs_UserId_Timestamp",
                table: "AssistantInteractionLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageMetrics_Date",
                table: "AssistantUsageMetrics",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantUsageMetrics_GuildId_Date_Unique",
                table: "AssistantUsageMetrics",
                columns: new[] { "GuildId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantGuildSettings");

            migrationBuilder.DropTable(
                name: "AssistantInteractionLogs");

            migrationBuilder.DropTable(
                name: "AssistantUsageMetrics");
        }
    }
}
