using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageContextToModerationCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "ContextChannelId",
                table: "ModerationCases",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContextMessageContent",
                table: "ModerationCases",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<ulong>(
                name: "ContextMessageId",
                table: "ModerationCases",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContextChannelId",
                table: "ModerationCases");

            migrationBuilder.DropColumn(
                name: "ContextMessageContent",
                table: "ModerationCases");

            migrationBuilder.DropColumn(
                name: "ContextMessageId",
                table: "ModerationCases");
        }
    }
}
