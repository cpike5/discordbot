using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreferredThemeId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Themes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ThemeKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ColorDefinition = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Themes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PreferredThemeId",
                table: "AspNetUsers",
                column: "PreferredThemeId");

            migrationBuilder.CreateIndex(
                name: "IX_Themes_IsActive",
                table: "Themes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Themes_ThemeKey",
                table: "Themes",
                column: "ThemeKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Themes_PreferredThemeId",
                table: "AspNetUsers",
                column: "PreferredThemeId",
                principalTable: "Themes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Seed Discord Dark theme
            migrationBuilder.InsertData(
                table: "Themes",
                columns: new[] { "ThemeKey", "DisplayName", "Description", "ColorDefinition", "IsActive", "CreatedAt" },
                values: new object[]
                {
                    "discord-dark",
                    "Discord Dark",
                    "Dark theme inspired by Discord's interface with orange and blue accents.",
                    """{"bgPrimary":"#1d2022","bgSecondary":"#262a2d","bgTertiary":"#2f3336","bgHover":"#3a3f42","textPrimary":"#d7d3d0","textSecondary":"#a8a5a3","textTertiary":"#7a7876","textInverse":"#1d2022","accentOrange":"#cb4e1b","accentOrangeHover":"#e55d22","accentOrangeActive":"#b3440f","accentOrangeMuted":"#cb4e1b33","accentBlue":"#098ecf","accentBlueHover":"#0aa5ed","accentBlueActive":"#0778ab","accentBlueMuted":"#098ecf33","borderPrimary":"#3a3f42","borderSecondary":"#2f3336","borderFocus":"#cb4e1b"}""",
                    true,
                    DateTime.UtcNow
                });

            // Seed Purple Dusk theme
            migrationBuilder.InsertData(
                table: "Themes",
                columns: new[] { "ThemeKey", "DisplayName", "Description", "ColorDefinition", "IsActive", "CreatedAt" },
                values: new object[]
                {
                    "purple-dusk",
                    "Purple Dusk",
                    "Light theme with warm beige backgrounds and purple accents.",
                    """{"bgPrimary":"#E8E3DF","bgSecondary":"#DAD4D0","bgTertiary":"#CCC5C0","bgHover":"#C0B8B2","textPrimary":"#4F214A","textSecondary":"#614978","textTertiary":"#887A99","textInverse":"#E8E3DF","accentPurple":"#614978","accentPurpleHover":"#7A5C8F","accentPurpleActive":"#4F214A","accentPurpleMuted":"#61497833","accentPink":"#D5345B","accentPinkHover":"#E5476D","accentPinkActive":"#B82A4D","accentPinkMuted":"#D5345B33","borderPrimary":"#C0B8B2","borderSecondary":"#DAD4D0","borderFocus":"#614978"}""",
                    true,
                    DateTime.UtcNow
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Themes_PreferredThemeId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Themes");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PreferredThemeId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PreferredThemeId",
                table: "AspNetUsers");
        }
    }
}
