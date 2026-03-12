using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Postgresql
{
    /// <inheritdoc />
    public partial class AddVoxMessageHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VoxMessageHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClipGroup = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WordGapMs = table.Column<int>(type: "integer", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    PlayedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoxMessageHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoxMessageHistory_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoxMessageHistory_GuildId",
                table: "VoxMessageHistory",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_VoxMessageHistory_UserId_GuildId_IsFavorite",
                table: "VoxMessageHistory",
                columns: new[] { "UserId", "GuildId", "IsFavorite" });

            migrationBuilder.CreateIndex(
                name: "IX_VoxMessageHistory_UserId_GuildId_PlayedAt",
                table: "VoxMessageHistory",
                columns: new[] { "UserId", "GuildId", "PlayedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VoxMessageHistory");
        }
    }
}
