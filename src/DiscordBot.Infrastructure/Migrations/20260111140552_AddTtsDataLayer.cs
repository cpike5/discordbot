using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTtsDataLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuildTtsSettings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    TtsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    DefaultVoice = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: ""),
                    DefaultSpeed = table.Column<double>(type: "REAL", nullable: false, defaultValue: 1.0),
                    DefaultPitch = table.Column<double>(type: "REAL", nullable: false, defaultValue: 1.0),
                    DefaultVolume = table.Column<double>(type: "REAL", nullable: false, defaultValue: 0.80000000000000004),
                    MaxMessageLength = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 500),
                    RateLimitPerMinute = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                    AutoPlayOnSend = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    AnnounceJoinsLeaves = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildTtsSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildTtsSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TtsMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Voice = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TtsMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TtsMessages_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TtsMessages_CreatedAt",
                table: "TtsMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TtsMessages_GuildId_CreatedAt",
                table: "TtsMessages",
                columns: new[] { "GuildId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TtsMessages_GuildId_UserId_CreatedAt",
                table: "TtsMessages",
                columns: new[] { "GuildId", "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildTtsSettings");

            migrationBuilder.DropTable(
                name: "TtsMessages");
        }
    }
}
