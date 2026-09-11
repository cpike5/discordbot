using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using DiscordBot.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace DiscordBot.Tests.Services;

/// <summary>
/// One in-memory database with the currency repositories and services wired over it.
/// <para>
/// PR 1 registers nothing in <c>Program.cs</c>, so these are constructed by hand rather than
/// resolved from a container.
/// </para>
/// </summary>
internal sealed class CurrencyTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public CurrencyTestContext()
    {
        (Db, _connection) = TestDbContextFactory.CreateContext();

        Currencies = new CurrencyRepository(Db, NullLogger<CurrencyRepository>.Instance, NullLogger<Repository<Currency>>.Instance);
        Wallets = new WalletRepository(Db, NullLogger<WalletRepository>.Instance, NullLogger<Repository<Wallet>>.Instance);
        Ledger = new LedgerRepository(Db, NullLogger<LedgerRepository>.Instance);
        Prices = new PriceRepository(Db, NullLogger<PriceRepository>.Instance, NullLogger<Repository<PriceEntry>>.Instance);
        Authorities = new MintAuthorityRepository(Db, NullLogger<MintAuthorityRepository>.Instance, NullLogger<Repository<MintAuthority>>.Instance);

        WalletService = new WalletService(Currencies, Wallets, Ledger, NullLogger<WalletService>.Instance);
        CurrencyService = new CurrencyService(Currencies, Authorities, Prices, Wallets, Ledger, NullLogger<CurrencyService>.Instance);
    }

    public BotDbContext Db { get; }

    public CurrencyRepository Currencies { get; }

    public WalletRepository Wallets { get; }

    public LedgerRepository Ledger { get; }

    public PriceRepository Prices { get; }

    public MintAuthorityRepository Authorities { get; }

    public WalletService WalletService { get; }

    public CurrencyService CurrencyService { get; }

    /// <summary>Inserts a currency directly, bypassing the service's validation.</summary>
    public async Task<Currency> SeedCurrencyAsync(
        CurrencyScope scope = CurrencyScope.Guild,
        ulong? guildId = 1001UL,
        bool isTransferable = true,
        bool allowNegative = false,
        long? debtFloor = null,
        bool isActive = true,
        string name = "Coins")
    {
        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            GuildId = scope == CurrencyScope.Guild ? guildId : null,
            Name = name,
            Symbol = "C",
            IsTransferable = isTransferable,
            AllowNegative = allowNegative,
            DebtFloor = debtFloor,
            IsActive = isActive,
            CreatedById = 9000UL,
            CreatedAt = DateTime.UtcNow
        };

        Db.Currencies.Add(currency);
        await Db.SaveChangesAsync();
        return currency;
    }

    /// <summary>Gives a user a starting balance through the ledger, the way a mint would.</summary>
    public async Task<Wallet> SeedBalanceAsync(Guid currencyId, ulong userId, long balance)
    {
        var wallet = await Wallets.GetOrCreateAsync(currencyId, userId);

        if (balance != 0)
        {
            await Ledger.AppendAsync(new LedgerTransaction
            {
                WalletId = wallet.Id,
                Type = balance > 0 ? LedgerTransactionType.Mint : LedgerTransactionType.Fine,
                Source = LedgerSource.Manual,
                Amount = balance,
                Reason = "seed",
                IdempotencyKey = $"seed:{wallet.Id:N}:{Guid.NewGuid():N}"
            });
        }

        return wallet;
    }

    public async Task<long> BalanceOfAsync(Guid currencyId, ulong userId)
    {
        var wallet = await Wallets.GetAsync(currencyId, userId);
        return wallet?.CachedBalance ?? 0;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
