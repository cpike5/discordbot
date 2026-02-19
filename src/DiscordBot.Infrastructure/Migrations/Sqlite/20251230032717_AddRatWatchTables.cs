using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRatWatchTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuildRatWatchSettings",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Timezone = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, defaultValue: "UTC"),
                    MaxAdvanceHours = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 24),
                    VotingDurationMinutes = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 5),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildRatWatchSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildRatWatchSettings_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatWatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    AccusedUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    InitiatorUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    OriginalMessageId = table.Column<long>(type: "INTEGER", nullable: false),
                    CustomMessage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ScheduledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    NotificationMessageId = table.Column<long>(type: "INTEGER", nullable: true),
                    VotingMessageId = table.Column<long>(type: "INTEGER", nullable: true),
                    ClearedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VotingStartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    VotingEndedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatWatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatWatches_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RatWatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    GuiltyVotes = table.Column<int>(type: "INTEGER", nullable: false),
                    NotGuiltyVotes = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OriginalMessageLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatRecords_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RatRecords_RatWatches_RatWatchId",
                        column: x => x.RatWatchId,
                        principalTable: "RatWatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RatVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RatWatchId = table.Column<Guid>(type: "TEXT", nullable: false),
                    VoterUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsGuiltyVote = table.Column<bool>(type: "INTEGER", nullable: false),
                    VotedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatVotes_RatWatches_RatWatchId",
                        column: x => x.RatWatchId,
                        principalTable: "RatWatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RatRecords_GuildId_UserId",
                table: "RatRecords",
                columns: new[] { "GuildId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RatRecords_RatWatchId",
                table: "RatRecords",
                column: "RatWatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatRecords_RecordedAt",
                table: "RatRecords",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RatVotes_RatWatchId",
                table: "RatVotes",
                column: "RatWatchId");

            migrationBuilder.CreateIndex(
                name: "IX_RatVotes_RatWatchId_VoterUserId_Unique",
                table: "RatVotes",
                columns: new[] { "RatWatchId", "VoterUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RatWatches_ChannelId",
                table: "RatWatches",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_RatWatches_GuildId_AccusedUserId",
                table: "RatWatches",
                columns: new[] { "GuildId", "AccusedUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_RatWatches_GuildId_ScheduledAt_Status",
                table: "RatWatches",
                columns: new[] { "GuildId", "ScheduledAt", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildRatWatchSettings");

            migrationBuilder.DropTable(
                name: "RatRecords");

            migrationBuilder.DropTable(
                name: "RatVotes");

            migrationBuilder.DropTable(
                name: "RatWatches");
        }
    }
}
