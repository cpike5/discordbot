using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddUserTtsPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserTtsPresets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    VoiceName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Style = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Speed = table.Column<decimal>(type: "TEXT", nullable: false),
                    Pitch = table.Column<decimal>(type: "TEXT", nullable: false),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTtsPresets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTtsPresets_UserId",
                table: "UserTtsPresets",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTtsPresets");
        }
    }
}
