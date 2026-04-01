using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddNotXGuildSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotXGuildSettings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    OutputChannelId = table.Column<long>(type: "INTEGER", nullable: true),
                    MonitoredChannelIdsJson = table.Column<string>(type: "TEXT", nullable: true),
                    SensitiveOnly = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    HideSensitiveLabel = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotXGuildSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_NotXGuildSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotXGuildSettings");
        }
    }
}
