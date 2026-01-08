using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActivityEvents : Migration
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
                    EventType = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserActivityEvents_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserActivityEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_GuildId_ChannelId_Timestamp",
                table: "UserActivityEvents",
                columns: new[] { "GuildId", "ChannelId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_GuildId_EventType_Timestamp",
                table: "UserActivityEvents",
                columns: new[] { "GuildId", "EventType", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_GuildId_Timestamp",
                table: "UserActivityEvents",
                columns: new[] { "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_GuildId_UserId_Timestamp",
                table: "UserActivityEvents",
                columns: new[] { "GuildId", "UserId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_Timestamp",
                table: "UserActivityEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivityEvents_UserId",
                table: "UserActivityEvents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserActivityEvents");
        }
    }
}
