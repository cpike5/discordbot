using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDiscordGuilds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserDiscordGuilds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GuildIconHash = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsOwner = table.Column<bool>(type: "INTEGER", nullable: false),
                    Permissions = table.Column<long>(type: "INTEGER", nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDiscordGuilds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserDiscordGuilds_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserDiscordGuilds_ApplicationUserId_GuildId",
                table: "UserDiscordGuilds",
                columns: new[] { "ApplicationUserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserDiscordGuilds_GuildId",
                table: "UserDiscordGuilds",
                column: "GuildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserDiscordGuilds");
        }
    }
}
