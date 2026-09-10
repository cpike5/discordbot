using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Postgresql
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
                    Id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Vendor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ContextLength = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    PromptPricePerMillion = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    CompletionPricePerMillion = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    CacheReadPricePerMillion = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    CacheWritePricePerMillion = table.Column<decimal>(type: "numeric(18,8)", nullable: true),
                    SupportsTools = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SupportsImages = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    EnabledAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EnabledBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
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
