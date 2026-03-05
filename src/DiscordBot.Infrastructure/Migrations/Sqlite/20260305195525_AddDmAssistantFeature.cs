using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddDmAssistantFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DmAssistantInteractionLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsOwner = table.Column<bool>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Response = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CachedTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LatencyMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,8)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DmAssistantInteractionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DmAssistantInteractionLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DmAssistantUsageMetrics",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalMessages = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalInputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalOutputTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    TotalCachedTokens = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    EstimatedCostUsd = table.Column<decimal>(type: "decimal(18,8)", nullable: false, defaultValue: 0m),
                    FailedRequests = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    AverageLatencyMs = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DmAssistantUsageMetrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DmAssistantUsageMetrics_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DmConversationMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DmConversationMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DmConversationMessages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DmAssistantInteractionLogs_Timestamp",
                table: "DmAssistantInteractionLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_DmAssistantInteractionLogs_UserId_Timestamp",
                table: "DmAssistantInteractionLogs",
                columns: new[] { "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_DmAssistantUsageMetrics_UserId_Date",
                table: "DmAssistantUsageMetrics",
                columns: new[] { "UserId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DmConversationMessages_UserId_Timestamp",
                table: "DmConversationMessages",
                columns: new[] { "UserId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DmAssistantInteractionLogs");

            migrationBuilder.DropTable(
                name: "DmAssistantUsageMetrics");

            migrationBuilder.DropTable(
                name: "DmConversationMessages");
        }
    }
}
