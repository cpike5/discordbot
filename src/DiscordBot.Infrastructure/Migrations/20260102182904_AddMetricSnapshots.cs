using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMetricSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MetricSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DatabaseAvgQueryTimeMs = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    DatabaseTotalQueries = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    DatabaseSlowQueryCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    WorkingSetMB = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    PrivateMemoryMB = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    HeapSizeMB = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    Gen0Collections = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Gen1Collections = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Gen2Collections = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CacheHitRatePercent = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.0),
                    CacheTotalEntries = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    CacheTotalHits = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    CacheTotalMisses = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    ServicesRunningCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ServicesErrorCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    ServicesTotalCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MetricSnapshots_Timestamp",
                table: "MetricSnapshots",
                column: "Timestamp",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MetricSnapshots");
        }
    }
}
