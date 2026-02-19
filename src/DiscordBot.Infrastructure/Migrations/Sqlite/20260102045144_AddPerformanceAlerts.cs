using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerformanceAlertConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MetricName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    WarningThreshold = table.Column<double>(type: "REAL", nullable: true),
                    CriticalThreshold = table.Column<double>(type: "REAL", nullable: true),
                    ThresholdUnit = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UpdatedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceAlertConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PerformanceIncidents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetricName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ThresholdValue = table.Column<double>(type: "REAL", nullable: false),
                    ActualValue = table.Column<double>(type: "REAL", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceIncidents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAlertConfigs_IsEnabled",
                table: "PerformanceAlertConfigs",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceAlertConfigs_MetricName",
                table: "PerformanceAlertConfigs",
                column: "MetricName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIncidents_MetricName_TriggeredAt",
                table: "PerformanceIncidents",
                columns: new[] { "MetricName", "TriggeredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIncidents_Severity_Status",
                table: "PerformanceIncidents",
                columns: new[] { "Severity", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIncidents_Status",
                table: "PerformanceIncidents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PerformanceIncidents_TriggeredAt",
                table: "PerformanceIncidents",
                column: "TriggeredAt");

            // Seed default alert configurations
            var now = DateTime.UtcNow;
            migrationBuilder.InsertData(
                table: "PerformanceAlertConfigs",
                columns: new[] { "MetricName", "DisplayName", "Description", "WarningThreshold", "CriticalThreshold", "ThresholdUnit", "IsEnabled", "CreatedAt", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { "gateway_latency", "Gateway Latency", "Discord gateway heartbeat latency", 100.0, 200.0, "ms", true, now, null, null },
                    { "command_p95_latency", "Command P95 Latency", "95th percentile command response time", 300.0, 500.0, "ms", true, now, null, null },
                    { "error_rate", "Error Rate", "Percentage of failed command executions", 1.0, 5.0, "%", true, now, null, null },
                    { "memory_usage", "Memory Usage", "Working set memory consumption", 400.0, 480.0, "MB", true, now, null, null },
                    { "api_rate_limit_usage", "API Rate Limit", "Discord API rate limit capacity usage", 85.0, 95.0, "%", true, now, null, null },
                    { "database_query_time", "Database Query Time", "Average database query execution time", 50.0, 100.0, "ms", true, now, null, null },
                    { "bot_disconnected", "Bot Disconnected", "Bot gateway disconnection event", null, 1.0, "event", true, now, null, null },
                    { "service_failure", "Service Failure", "Background service failure", null, 1.0, "event", true, now, null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerformanceAlertConfigs");

            migrationBuilder.DropTable(
                name: "PerformanceIncidents");
        }
    }
}
