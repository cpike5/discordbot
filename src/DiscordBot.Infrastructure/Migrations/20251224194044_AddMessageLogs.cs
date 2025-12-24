using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MessageLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DiscordMessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    AuthorId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LoggedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    HasAttachments = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    HasEmbeds = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ReplyToMessageId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageLogs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MessageLogs_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_AuthorId_Timestamp",
                table: "MessageLogs",
                columns: new[] { "AuthorId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_ChannelId_Timestamp",
                table: "MessageLogs",
                columns: new[] { "ChannelId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_DiscordMessageId_Unique",
                table: "MessageLogs",
                column: "DiscordMessageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_GuildId_Timestamp",
                table: "MessageLogs",
                columns: new[] { "GuildId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MessageLogs_LoggedAt",
                table: "MessageLogs",
                column: "LoggedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageLogs");
        }
    }
}
