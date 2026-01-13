using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandModuleConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommandModuleConfigurations",
                columns: table => new
                {
                    ModuleName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RequiresRestart = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandModuleConfigurations", x => x.ModuleName);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandModuleConfigurations_Category",
                table: "CommandModuleConfigurations",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CommandModuleConfigurations_IsEnabled",
                table: "CommandModuleConfigurations",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_CommandModuleConfigurations_LastModifiedAt",
                table: "CommandModuleConfigurations",
                column: "LastModifiedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandModuleConfigurations");
        }
    }
}
