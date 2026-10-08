using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class AddModLogChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ModLogChannelId",
                table: "GuildModerationConfigs",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ModLogEvents",
                table: "GuildModerationConfigs",
                type: "integer",
                nullable: false,
                // Existing rows get every kind, so a guild that later picks a channel starts with all
                // three toggles on, as a new row does from the entity's initializer.
                defaultValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModLogChannelId",
                table: "GuildModerationConfigs");

            migrationBuilder.DropColumn(
                name: "ModLogEvents",
                table: "GuildModerationConfigs");
        }
    }
}
