using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    Settings = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    Username = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Discriminator = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false, defaultValue: "0"),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CommandLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResponseTimeMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandLogs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CommandLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_CommandName",
                table: "CommandLogs",
                column: "CommandName");

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_ExecutedAt",
                table: "CommandLogs",
                column: "ExecutedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_GuildId",
                table: "CommandLogs",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_Success",
                table: "CommandLogs",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_UserId",
                table: "CommandLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Guilds_IsActive",
                table: "Guilds",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastSeenAt",
                table: "Users",
                column: "LastSeenAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandLogs");

            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
