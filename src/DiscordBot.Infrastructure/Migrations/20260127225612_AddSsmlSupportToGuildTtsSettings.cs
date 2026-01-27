using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSsmlSupportToGuildTtsSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultStyle",
                table: "GuildTtsSettings",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DefaultStyleDegree",
                table: "GuildTtsSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<int>(
                name: "MaxSsmlComplexity",
                table: "GuildTtsSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<bool>(
                name: "SsmlEnabled",
                table: "GuildTtsSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "StrictSsmlValidation",
                table: "GuildTtsSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultStyle",
                table: "GuildTtsSettings");

            migrationBuilder.DropColumn(
                name: "DefaultStyleDegree",
                table: "GuildTtsSettings");

            migrationBuilder.DropColumn(
                name: "MaxSsmlComplexity",
                table: "GuildTtsSettings");

            migrationBuilder.DropColumn(
                name: "SsmlEnabled",
                table: "GuildTtsSettings");

            migrationBuilder.DropColumn(
                name: "StrictSsmlValidation",
                table: "GuildTtsSettings");
        }
    }
}
