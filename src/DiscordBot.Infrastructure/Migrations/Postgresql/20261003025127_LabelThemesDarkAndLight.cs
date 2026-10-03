using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class LabelThemesDarkAndLight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UX decision D5: theme names say whether they are dark or light. The dark theme
            // also takes the design system's name for it, Graphite.
            migrationBuilder.UpdateData(
                table: "Themes",
                keyColumn: "ThemeKey",
                keyValue: "discord-dark",
                columns: new[] { "DisplayName", "Description" },
                values: new object[] { "Graphite (dark)", "Dark graphite surfaces with an ember accent. The default theme." });

            migrationBuilder.UpdateData(
                table: "Themes",
                keyColumn: "ThemeKey",
                keyValue: "purple-dusk",
                columns: new[] { "DisplayName", "Description" },
                values: new object[] { "Purple Dusk (light)", "Light theme with warm paper surfaces and plum accents." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Themes",
                keyColumn: "ThemeKey",
                keyValue: "discord-dark",
                columns: new[] { "DisplayName", "Description" },
                values: new object[] { "Discord Dark", "Dark theme inspired by Discord's interface with orange and blue accents." });

            migrationBuilder.UpdateData(
                table: "Themes",
                keyColumn: "ThemeKey",
                keyValue: "purple-dusk",
                columns: new[] { "DisplayName", "Description" },
                values: new object[] { "Purple Dusk", "Light theme with warm beige backgrounds and purple accents." });
        }
    }
}
