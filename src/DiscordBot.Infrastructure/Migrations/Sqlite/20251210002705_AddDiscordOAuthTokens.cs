using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordOAuthTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscordOAuthTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApplicationUserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    EncryptedAccessToken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EncryptedRefreshToken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Scopes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastRefreshedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    DiscordUserId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscordOAuthTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscordOAuthTokens_AspNetUsers_ApplicationUserId",
                        column: x => x.ApplicationUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscordOAuthTokens_AccessTokenExpiresAt",
                table: "DiscordOAuthTokens",
                column: "AccessTokenExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_DiscordOAuthTokens_ApplicationUserId",
                table: "DiscordOAuthTokens",
                column: "ApplicationUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscordOAuthTokens_DiscordUserId",
                table: "DiscordOAuthTokens",
                column: "DiscordUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscordOAuthTokens");
        }
    }
}
