using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordBot.Infrastructure.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddVirtualCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Scope = table.Column<int>(type: "INTEGER", nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    IsTransferable = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    AllowNegative = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    DebtFloor = table.Column<long>(type: "INTEGER", nullable: true),
                    IncomeAmount = table.Column<long>(type: "INTEGER", nullable: true),
                    IncomeInterval = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedById = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MintAuthorities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PrincipalType = table.Column<int>(type: "INTEGER", nullable: false),
                    PrincipalId = table.Column<long>(type: "INTEGER", nullable: true),
                    GrantedById = table.Column<long>(type: "INTEGER", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MintAuthorities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MintAuthorities_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PriceEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FeatureKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    GuildId = table.Column<long>(type: "INTEGER", nullable: true),
                    CurrencyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    ExemptRoleIds = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    UpdatedById = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceEntries_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Wallets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    CachedBalance = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wallets_Currencies_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WalletId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    BalanceAfter = table.Column<long>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    FeatureKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ReferenceTransactionId = table.Column<long>(type: "INTEGER", nullable: true),
                    ModerationCaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActorId = table.Column<long>(type: "INTEGER", nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LedgerTransactions_Wallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "Wallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_GuildId",
                table: "Currencies",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_Scope_GuildId_Name",
                table: "Currencies",
                columns: new[] { "Scope", "GuildId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_IdempotencyKey",
                table: "LedgerTransactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_ReferenceTransactionId",
                table: "LedgerTransactions",
                column: "ReferenceTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_WalletId_CreatedAt",
                table: "LedgerTransactions",
                columns: new[] { "WalletId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MintAuthorities_CurrencyId_PrincipalType_PrincipalId",
                table: "MintAuthorities",
                columns: new[] { "CurrencyId", "PrincipalType", "PrincipalId" });

            migrationBuilder.CreateIndex(
                name: "IX_PriceEntries_CurrencyId",
                table: "PriceEntries",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceEntries_FeatureKey_GuildId",
                table: "PriceEntries",
                columns: new[] { "FeatureKey", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceEntries_GuildId",
                table: "PriceEntries",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_CurrencyId_CachedBalance",
                table: "Wallets",
                columns: new[] { "CurrencyId", "CachedBalance" });

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_CurrencyId_UserId",
                table: "Wallets",
                columns: new[] { "CurrencyId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_UserId",
                table: "Wallets",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LedgerTransactions");

            migrationBuilder.DropTable(
                name: "MintAuthorities");

            migrationBuilder.DropTable(
                name: "PriceEntries");

            migrationBuilder.DropTable(
                name: "Wallets");

            migrationBuilder.DropTable(
                name: "Currencies");
        }
    }
}
