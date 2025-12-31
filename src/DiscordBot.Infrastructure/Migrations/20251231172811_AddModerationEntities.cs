using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlaggedEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<long>(type: "INTEGER", nullable: true),
                    RuleType = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Evidence = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ActionTaken = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ReviewedByUserId = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlaggedEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlaggedEvents_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildModerationConfigs",
                columns: table => new
                {
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    SimplePreset = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SpamConfig = table.Column<string>(type: "TEXT", nullable: false),
                    ContentFilterConfig = table.Column<string>(type: "TEXT", nullable: false),
                    RaidProtectionConfig = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildModerationConfigs", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildModerationConfigs_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    TargetUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    AuthorUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModNotes_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFromTemplate = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModTags_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Watchlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    AddedByUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Watchlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Watchlists_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModerationCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CaseNumber = table.Column<long>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    TargetUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    ModeratorUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RelatedFlaggedEventId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModerationCases_FlaggedEvents_RelatedFlaggedEventId",
                        column: x => x.RelatedFlaggedEventId,
                        principalTable: "FlaggedEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ModerationCases_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserModTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    TagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AppliedByUserId = table.Column<long>(type: "INTEGER", nullable: false),
                    AppliedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserModTags_ModTags_TagId",
                        column: x => x.TagId,
                        principalTable: "ModTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlaggedEvents_GuildId_RuleType_CreatedAt",
                table: "FlaggedEvents",
                columns: new[] { "GuildId", "RuleType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FlaggedEvents_GuildId_Severity_Status",
                table: "FlaggedEvents",
                columns: new[] { "GuildId", "Severity", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FlaggedEvents_GuildId_Status_CreatedAt",
                table: "FlaggedEvents",
                columns: new[] { "GuildId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FlaggedEvents_GuildId_UserId_CreatedAt",
                table: "FlaggedEvents",
                columns: new[] { "GuildId", "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_ExpiresAt_Type",
                table: "ModerationCases",
                columns: new[] { "ExpiresAt", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_GuildId_CaseNumber",
                table: "ModerationCases",
                columns: new[] { "GuildId", "CaseNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_GuildId_ModeratorUserId_CreatedAt",
                table: "ModerationCases",
                columns: new[] { "GuildId", "ModeratorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_GuildId_TargetUserId_CreatedAt",
                table: "ModerationCases",
                columns: new[] { "GuildId", "TargetUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_GuildId_Type_CreatedAt",
                table: "ModerationCases",
                columns: new[] { "GuildId", "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationCases_RelatedFlaggedEventId",
                table: "ModerationCases",
                column: "RelatedFlaggedEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ModNotes_GuildId_AuthorUserId_CreatedAt",
                table: "ModNotes",
                columns: new[] { "GuildId", "AuthorUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModNotes_GuildId_TargetUserId_CreatedAt",
                table: "ModNotes",
                columns: new[] { "GuildId", "TargetUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ModTags_GuildId_Category",
                table: "ModTags",
                columns: new[] { "GuildId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_ModTags_GuildId_Name",
                table: "ModTags",
                columns: new[] { "GuildId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserModTags_GuildId_UserId",
                table: "UserModTags",
                columns: new[] { "GuildId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserModTags_GuildId_UserId_TagId",
                table: "UserModTags",
                columns: new[] { "GuildId", "UserId", "TagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserModTags_TagId_AppliedAt",
                table: "UserModTags",
                columns: new[] { "TagId", "AppliedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_GuildId_AddedAt",
                table: "Watchlists",
                columns: new[] { "GuildId", "AddedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_GuildId_AddedByUserId",
                table: "Watchlists",
                columns: new[] { "GuildId", "AddedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Watchlists_GuildId_UserId",
                table: "Watchlists",
                columns: new[] { "GuildId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildModerationConfigs");

            migrationBuilder.DropTable(
                name: "ModerationCases");

            migrationBuilder.DropTable(
                name: "ModNotes");

            migrationBuilder.DropTable(
                name: "UserModTags");

            migrationBuilder.DropTable(
                name: "Watchlists");

            migrationBuilder.DropTable(
                name: "FlaggedEvents");

            migrationBuilder.DropTable(
                name: "ModTags");
        }
    }
}
