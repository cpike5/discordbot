using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSoundboardEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuildAudioSettings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    AudioEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    AutoLeaveTimeoutMinutes = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                    QueueEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    MaxDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 30),
                    MaxFileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 5242880L),
                    MaxSoundsPerGuild = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 50),
                    MaxStorageBytes = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 104857600L),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildAudioSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildAudioSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<double>(type: "REAL", nullable: false),
                    UploadedById = table.Column<long>(type: "INTEGER", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PlayCount = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sounds_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommandRoleRestrictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    CommandName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    AllowedRoleIds = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandRoleRestrictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommandRoleRestrictions_GuildAudioSettings_GuildId",
                        column: x => x.GuildId,
                        principalTable: "GuildAudioSettings",
                        principalColumn: "GuildId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandRoleRestrictions_GuildId_CommandName",
                table: "CommandRoleRestrictions",
                columns: new[] { "GuildId", "CommandName" });

            migrationBuilder.CreateIndex(
                name: "IX_Sounds_GuildId",
                table: "Sounds",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Sounds_GuildId_Name",
                table: "Sounds",
                columns: new[] { "GuildId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandRoleRestrictions");

            migrationBuilder.DropTable(
                name: "Sounds");

            migrationBuilder.DropTable(
                name: "GuildAudioSettings");
        }
    }
}
