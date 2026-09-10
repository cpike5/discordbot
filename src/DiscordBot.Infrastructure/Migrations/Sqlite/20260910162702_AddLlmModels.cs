using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddLlmModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlmModels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Vendor = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ContextLength = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    PromptPricePerMillion = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    CompletionPricePerMillion = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    CacheReadPricePerMillion = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    CacheWritePricePerMillion = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    SupportsTools = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    SupportsImages = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ReleasedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    EnabledAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EnabledBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmModels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmModels_IsEnabled",
                table: "LlmModels",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_LlmModels_Vendor",
                table: "LlmModels",
                column: "Vendor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlmModels");
        }
    }
}
