using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class RaiseMemoryAlertDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UX decision D14: the seeded memory alert (400 MB warning, 480 MB critical) sat below
            // the bot's idle working set (about 620 MB), so a fresh install raised a critical alert
            // at once. Raise the defaults, but only for a row an operator has not touched.
            migrationBuilder.Sql(
                @"UPDATE ""PerformanceAlertConfigs""
                  SET ""WarningThreshold"" = 1024, ""CriticalThreshold"" = 1536
                  WHERE ""MetricName"" = 'memory_usage'
                    AND ""UpdatedAt"" IS NULL
                    AND ""WarningThreshold"" = 400
                    AND ""CriticalThreshold"" = 480;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"UPDATE ""PerformanceAlertConfigs""
                  SET ""WarningThreshold"" = 400, ""CriticalThreshold"" = 480
                  WHERE ""MetricName"" = 'memory_usage'
                    AND ""UpdatedAt"" IS NULL
                    AND ""WarningThreshold"" = 1024
                    AND ""CriticalThreshold"" = 1536;");
        }
    }
}
