using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddAssistantThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ThreadId",
                table: "AssistantInteractionLogs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConversationMode",
                table: "AssistantGuildSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AssistantThreads",
                columns: table => new
                {
                    ThreadId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ParentChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    StarterUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TurnCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveSkills = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantThreads", x => x.ThreadId);
                    table.ForeignKey(
                        name: "FK_AssistantThreads_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AssistantThreadMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ThreadId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantThreadMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssistantThreadMessages_AssistantThreads_ThreadId",
                        column: x => x.ThreadId,
                        principalTable: "AssistantThreads",
                        principalColumn: "ThreadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantThreadMessages_ThreadId_Id",
                table: "AssistantThreadMessages",
                columns: new[] { "ThreadId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantThreadMessages_UserId",
                table: "AssistantThreadMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantThreads_GuildId",
                table: "AssistantThreads",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantThreads_LastActivityAt",
                table: "AssistantThreads",
                column: "LastActivityAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantThreadMessages");

            migrationBuilder.DropTable(
                name: "AssistantThreads");

            migrationBuilder.DropColumn(
                name: "ThreadId",
                table: "AssistantInteractionLogs");

            migrationBuilder.DropColumn(
                name: "ConversationMode",
                table: "AssistantGuildSettings");
        }
    }
}
