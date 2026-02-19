using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActivityEventTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserActivityEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivityEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_EventType_GuildId_Timestamp",
                table: "UserActivityEvents",
                columns: new[] { "EventType", "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_GuildId_Timestamp",
                table: "UserActivityEvents",
                columns: new[] { "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_LoggedAt",
                table: "UserActivityEvents",
                column: "LoggedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_UserId_Timestamp",
                table: "UserActivityEvents",
                columns: new[] { "UserId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserActivityEvents");
        }
    }
}
