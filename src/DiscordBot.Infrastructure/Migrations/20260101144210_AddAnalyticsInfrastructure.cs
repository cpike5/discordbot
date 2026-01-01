using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChannelActivitySnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Granularity = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UniqueUsers = table.Column<int>(type: "INTEGER", nullable: false),
                    PeakHour = table.Column<int>(type: "INTEGER", nullable: true),
                    PeakHourMessageCount = table.Column<int>(type: "INTEGER", nullable: true),
                    AverageMessageLength = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelActivitySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelActivitySnapshots_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildMetricsSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    TotalMembers = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveMembers = table.Column<int>(type: "INTEGER", nullable: false),
                    MembersJoined = table.Column<int>(type: "INTEGER", nullable: false),
                    MembersLeft = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalMessages = table.Column<int>(type: "INTEGER", nullable: false),
                    CommandsExecuted = table.Column<int>(type: "INTEGER", nullable: false),
                    ModerationActions = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveChannels = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalVoiceMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildMetricsSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuildMetricsSnapshots_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberActivitySnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Granularity = table.Column<int>(type: "INTEGER", nullable: false),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ReactionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    VoiceMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    UniqueChannelsActive = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberActivitySnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberActivitySnapshots_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberActivitySnapshots_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelActivitySnapshots_Guild_Channel_Period",
                table: "ChannelActivitySnapshots",
                columns: new[] { "GuildId", "ChannelId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelActivitySnapshots_Guild_Period_Granularity",
                table: "ChannelActivitySnapshots",
                columns: new[] { "GuildId", "PeriodStart", "Granularity" });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelActivitySnapshots_Unique",
                table: "ChannelActivitySnapshots",
                columns: new[] { "GuildId", "ChannelId", "PeriodStart", "Granularity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildMetricsSnapshots_Unique",
                table: "GuildMetricsSnapshots",
                columns: new[] { "GuildId", "SnapshotDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivitySnapshots_Guild_Period_Granularity",
                table: "MemberActivitySnapshots",
                columns: new[] { "GuildId", "PeriodStart", "Granularity" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivitySnapshots_Guild_User_Period",
                table: "MemberActivitySnapshots",
                columns: new[] { "GuildId", "UserId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivitySnapshots_Unique",
                table: "MemberActivitySnapshots",
                columns: new[] { "GuildId", "UserId", "PeriodStart", "Granularity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberActivitySnapshots_UserId",
                table: "MemberActivitySnapshots",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelActivitySnapshots");

            migrationBuilder.DropTable(
                name: "GuildMetricsSnapshots");

            migrationBuilder.DropTable(
                name: "MemberActivitySnapshots");
        }
    }
}
