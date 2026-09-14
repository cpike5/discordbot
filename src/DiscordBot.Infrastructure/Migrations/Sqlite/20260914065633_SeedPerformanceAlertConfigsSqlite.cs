using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <summary>
    /// Re-seeds the eight default <c>PerformanceAlertConfigs</c> rows that the pre-split
    /// <c>AddPerformanceAlerts</c> migration originally inserted. Same root cause as its sibling
    /// <see cref="SeedDefaultThemesSqlite"/>: the SqliteBotDbContext baseline
    /// (<c>20260219205009_AddIsEnabledToGuildModerationConfig</c>) was scaffolded as a
    /// model diff, so it re-creates the <c>PerformanceAlertConfigs</c> table but carries none of
    /// the original <c>InsertData</c> calls - seed rows are not part of a model snapshot. A
    /// from-scratch Sqlite database therefore came up with an empty alert-configuration table and
    /// the alerting UI/evaluator had nothing to threshold against. The Postgres baseline
    /// (<c>Migrations/Postgresql/20260219132220_InitialPostgresql.cs</c>) does seed these rows; the
    /// values below are copied from it verbatim so both providers start identical.
    /// </summary>
    /// <remarks>
    /// <c>INSERT OR IGNORE</c> keyed on the unique <c>IX_PerformanceAlertConfigs_MetricName</c>
    /// index, so this is a no-op (not a unique-constraint failure) on a database whose history
    /// predates the split and which therefore already carries these rows, and on any database where
    /// an operator has already created a config for one of these metric names.
    /// </remarks>
    public partial class SeedPerformanceAlertConfigsSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Values mirror Migrations/Postgresql/20260219132220_InitialPostgresql.cs exactly:
            // MetricName, DisplayName, Description, WarningThreshold, CriticalThreshold, ThresholdUnit.
            // IsEnabled is 1 (true) and UpdatedAt/UpdatedBy stay NULL, as there.
            migrationBuilder.Sql("""
                INSERT OR IGNORE INTO PerformanceAlertConfigs
                    (MetricName, DisplayName, Description, WarningThreshold, CriticalThreshold, ThresholdUnit, IsEnabled, CreatedAt, UpdatedAt, UpdatedBy)
                VALUES
                    ('gateway_latency', 'Gateway Latency', 'Discord gateway heartbeat latency', 100.0, 200.0, 'ms', 1, CURRENT_TIMESTAMP, NULL, NULL),
                    ('command_p95_latency', 'Command P95 Latency', '95th percentile command response time', 300.0, 500.0, 'ms', 1, CURRENT_TIMESTAMP, NULL, NULL),
                    ('error_rate', 'Error Rate', 'Percentage of failed command executions', 1.0, 5.0, '%', 1, CURRENT_TIMESTAMP, NULL, NULL),
                    ('memory_usage', 'Memory Usage', 'Working set memory consumption', 400.0, 480.0, 'MB', 1, CURRENT_TIMESTAMP, NULL, NULL),
                    ('api_rate_limit_usage', 'API Rate Limit', 'Discord API rate limit capacity usage', 85.0, 95.0, '%', 1, CURRENT_TIMESTAMP, NULL, NULL),
                    ('database_query_time', 'Database Query Time', 'Average database query execution time', 50.0, 100.0, 'ms', 1, CURRENT_TIMESTAMP, NULL, NULL),
                    ('bot_disconnected', 'Bot Disconnected', 'Bot gateway disconnection event', NULL, 1.0, 'event', 1, CURRENT_TIMESTAMP, NULL, NULL),
                    ('service_failure', 'Service Failure', 'Background service failure', NULL, 1.0, 'event', 1, CURRENT_TIMESTAMP, NULL, NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM PerformanceAlertConfigs
                WHERE MetricName IN (
                    'gateway_latency', 'command_p95_latency', 'error_rate', 'memory_usage',
                    'api_rate_limit_usage', 'database_query_time', 'bot_disconnected', 'service_failure');
                """);
        }
    }
}
