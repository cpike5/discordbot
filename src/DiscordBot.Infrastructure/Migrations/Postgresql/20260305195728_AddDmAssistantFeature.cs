using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Postgresql
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    IsOwner = table.Column<bool>(type: "boolean", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Response = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CachedTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Success = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(18,8)", nullable: false, defaultValue: 0m)
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalMessages = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalInputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalOutputTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    TotalCachedTokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(18,8)", nullable: false, defaultValue: 0m),
                    FailedRequests = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AverageLatencyMs = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
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
