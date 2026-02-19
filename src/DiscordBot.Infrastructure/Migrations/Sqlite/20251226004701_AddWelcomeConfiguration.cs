using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWelcomeConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WelcomeConfigurations",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    WelcomeChannelId = table.Column<long>(type: "INTEGER", nullable: true),
                    WelcomeMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false, defaultValue: ""),
                    IncludeAvatar = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    UseEmbed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    EmbedColor = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WelcomeConfigurations", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_WelcomeConfigurations_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WelcomeConfigurations_IsEnabled",
                table: "WelcomeConfigurations",
                column: "IsEnabled");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WelcomeConfigurations");
        }
    }
}
