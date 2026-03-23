using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class AddTtsMessageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TtsMessageHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    VoiceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Style = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Speed = table.Column<decimal>(type: "numeric", nullable: false),
                    Pitch = table.Column<decimal>(type: "numeric", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PlayedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TtsMessageHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TtsMessageHistory_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TtsMessageHistory_GuildId",
                table: "TtsMessageHistory",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_TtsMessageHistory_UserId_GuildId_IsFavorite",
                table: "TtsMessageHistory",
                columns: new[] { "UserId", "GuildId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "IX_TtsMessageHistory_UserId_GuildId_PlayedAt",
                table: "TtsMessageHistory",
                columns: new[] { "UserId", "GuildId", "PlayedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TtsMessageHistory");
        }
    }
}
