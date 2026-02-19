using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildMemberEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AccountCreatedAt",
                table: "Users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarHash",
                table: "Users",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GlobalDisplayName",
                table: "Users",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GuildMembers",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Nickname = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CachedRolesJson = table.Column<string>(type: "TEXT", nullable: true),
                    LastActiveAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastCachedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildMembers", x => new { x.GuildId, x.UserId });
                    table.ForeignKey(
                        name: "FK_GuildMembers_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuildMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_GuildId",
                table: "GuildMembers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_GuildId_JoinedAt",
                table: "GuildMembers",
                columns: new[] { "GuildId", "JoinedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_GuildId_LastActiveAt",
                table: "GuildMembers",
                columns: new[] { "GuildId", "LastActiveAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_IsActive",
                table: "GuildMembers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_LastActiveAt",
                table: "GuildMembers",
                column: "LastActiveAt");

            migrationBuilder.CreateIndex(
                name: "IX_GuildMembers_UserId",
                table: "GuildMembers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildMembers");

            migrationBuilder.DropColumn(
                name: "AccountCreatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AvatarHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GlobalDisplayName",
                table: "Users");
        }
    }
}
