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
            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Sounds",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SoundCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoundCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoundCategories_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                    Speed = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Pitch = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
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
                name: "IX_Sounds_CategoryId",
                table: "Sounds",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundCategories_GuildId",
                table: "SoundCategories",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_SoundCategories_GuildId_Name",
                table: "SoundCategories",
                columns: new[] { "GuildId", "Name" },
                unique: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_Sounds_SoundCategories_CategoryId",
                table: "Sounds",
                column: "CategoryId",
                principalTable: "SoundCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sounds_SoundCategories_CategoryId",
                table: "Sounds");

            migrationBuilder.DropTable(
                name: "SoundCategories");

            migrationBuilder.DropTable(
                name: "TtsMessageHistory");

            migrationBuilder.DropIndex(
                name: "IX_Sounds_CategoryId",
                table: "Sounds");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Sounds");
        }
    }
}
