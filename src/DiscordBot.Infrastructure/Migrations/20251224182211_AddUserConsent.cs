using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserConsents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscordUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    ConsentType = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    RevokedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    GrantedVia = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RevokedVia = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConsents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserConsents_Users_DiscordUserId",
                        column: x => x.DiscordUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserConsents_DiscordUserId_ConsentType",
                table: "UserConsents",
                columns: new[] { "DiscordUserId", "ConsentType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserConsents_GrantedAt",
                table: "UserConsents",
                column: "GrantedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserConsents_RevokedAt",
                table: "UserConsents",
                column: "RevokedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserConsents");
        }
    }
}
