using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoundPlayLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SoundPlayLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoundPlayLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoundPlayLogs_Sounds_SoundId",
                        column: x => x.SoundId,
                        principalTable: "Sounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SoundPlayLogs_GuildId_PlayedAt",
                table: "SoundPlayLogs",
                columns: new[] { "GuildId", "PlayedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SoundPlayLogs_PlayedAt",
                table: "SoundPlayLogs",
                column: "PlayedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SoundPlayLogs_SoundId_PlayedAt",
                table: "SoundPlayLogs",
                columns: new[] { "SoundId", "PlayedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SoundPlayLogs");
        }
    }
}
