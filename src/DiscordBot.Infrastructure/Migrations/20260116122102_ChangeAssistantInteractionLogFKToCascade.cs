using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeAssistantInteractionLogFKToCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssistantInteractionLogs_Guilds_GuildId",
                table: "AssistantInteractionLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantInteractionLogs_Users_UserId",
                table: "AssistantInteractionLogs");

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantInteractionLogs_Guilds_GuildId",
                table: "AssistantInteractionLogs",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantInteractionLogs_Users_UserId",
                table: "AssistantInteractionLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssistantInteractionLogs_Guilds_GuildId",
                table: "AssistantInteractionLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AssistantInteractionLogs_Users_UserId",
                table: "AssistantInteractionLogs");

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantInteractionLogs_Guilds_GuildId",
                table: "AssistantInteractionLogs",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AssistantInteractionLogs_Users_UserId",
                table: "AssistantInteractionLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
