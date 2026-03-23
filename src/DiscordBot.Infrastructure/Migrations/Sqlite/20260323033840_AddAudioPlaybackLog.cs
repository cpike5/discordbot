using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddAudioPlaybackLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudioPlaybackLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    FeatureType = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: true),
                    PlayedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioPlaybackLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioPlaybackLogs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioPlaybackLogs_GuildId_PlayedAt",
                table: "AudioPlaybackLogs",
                columns: new[] { "GuildId", "PlayedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AudioPlaybackLogs_GuildId_UserId_PlayedAt",
                table: "AudioPlaybackLogs",
                columns: new[] { "GuildId", "UserId", "PlayedAt" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioPlaybackLogs");
        }
    }
}
