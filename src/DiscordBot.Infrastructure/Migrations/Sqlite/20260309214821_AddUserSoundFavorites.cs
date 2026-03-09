using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddUserSoundFavorites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSoundFavorites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    SoundId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FavoritedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSoundFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSoundFavorites_Sounds_SoundId",
                        column: x => x.SoundId,
                        principalTable: "Sounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSoundFavorites_SoundId",
                table: "UserSoundFavorites",
                column: "SoundId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSoundFavorites_UserId_GuildId",
                table: "UserSoundFavorites",
                columns: new[] { "UserId", "GuildId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSoundFavorites_UserId_SoundId_GuildId",
                table: "UserSoundFavorites",
                columns: new[] { "UserId", "SoundId", "GuildId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSoundFavorites");
        }
    }
}
