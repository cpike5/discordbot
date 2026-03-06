using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class AddDmInteractionToolCallTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LoopCount",
                table: "DmAssistantInteractionLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ToolCalls",
                table: "DmAssistantInteractionLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ToolNames",
                table: "DmAssistantInteractionLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LoopCount",
                table: "DmAssistantInteractionLogs");

            migrationBuilder.DropColumn(
                name: "ToolCalls",
                table: "DmAssistantInteractionLogs");

            migrationBuilder.DropColumn(
                name: "ToolNames",
                table: "DmAssistantInteractionLogs");
        }
    }
}
