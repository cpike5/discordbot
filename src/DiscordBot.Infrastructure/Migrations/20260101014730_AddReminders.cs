using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TriggerAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    LastError = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reminders_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_GuildId",
                table: "Reminders",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_Status_TriggerAt",
                table: "Reminders",
                columns: new[] { "Status", "TriggerAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reminders_UserId",
                table: "Reminders",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reminders");
        }
    }
}
