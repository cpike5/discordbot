using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Currency;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using DiscordBot.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace DiscordBot.Tests.Services;

/// <summary>
/// One in-memory database with the currency repositories and services wired over it, plus the
/// charge and mint seams and the in-memory hold store.
/// <para>
/// These are constructed by hand rather than resolved from a container, so a test can vary the
/// options (a zero hold expiry, for instance) without standing up the host.
/// </para>
/// </summary>
internal sealed class CurrencyTestContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MemoryCache _memoryCache;

    /// <param name="holdExpirySeconds">
    /// Reservation window for holds. Zero makes every hold expire the moment it is created, which
    /// is how the "an expired hold still commits" rule is exercised without waiting.
    /// </param>
    public CurrencyTestContext(int holdExpirySeconds = 120)
    {
        (Db, _connection) = TestDbContextFactory.CreateContext();

        Currencies = new CurrencyRepository(Db, NullLogger<CurrencyRepository>.Instance, NullLogger<Repository<Currency>>.Instance);
        Wallets = new WalletRepository(Db, NullLogger<WalletRepository>.Instance, NullLogger<Repository<Wallet>>.Instance);
        Ledger = new LedgerRepository(Db, NullLogger<LedgerRepository>.Instance);
        Prices = new PriceRepository(Db, NullLogger<PriceRepository>.Instance, NullLogger<Repository<PriceEntry>>.Instance);
        Authorities = new MintAuthorityRepository(Db, NullLogger<MintAuthorityRepository>.Instance, NullLogger<Repository<MintAuthority>>.Instance);

        AuditLog = new Mock<IAuditLogService>();
        AuditLogBuilder = CreateAuditLogBuilderMock();
        AuditLog.Setup(a => a.CreateBuilder()).Returns(AuditLogBuilder.Object);

        Members = new Mock<IGuildMemberService>();

        WalletService = new WalletService(Currencies, Wallets, Ledger, AuditLog.Object, NullLogger<WalletService>.Instance);
        CurrencyService = new CurrencyService(Currencies, Authorities, Prices, Wallets, Ledger, NullLogger<CurrencyService>.Instance);

        Options = new CurrencyOptions { HoldExpirySeconds = holdExpirySeconds };

        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        Cache = new InstrumentedMemoryCache(
            _memoryCache,
            NullLogger<InstrumentedMemoryCache>.Instance,
            MsOptions.Create(new PerformanceMetricsOptions()));

        Holds = new ChargeHoldStore(Cache, MsOptions.Create(Options), NullLogger<ChargeHoldStore>.Instance);

        ChargeService = new ChargeService(
            CurrencyService,
            Currencies,
            Wallets,
            WalletService,
            Ledger,
            Holds,
            Members.Object,
            NullLogger<ChargeService>.Instance);

        MintService = new MintService(
            Currencies,
            Authorities,
            WalletService,
            Members.Object,
            AuditLog.Object,
            NullLogger<MintService>.Instance);
    }

    public BotDbContext Db { get; }

    public CurrencyRepository Currencies { get; }

    public WalletRepository Wallets { get; }

    public LedgerRepository Ledger { get; }

    public PriceRepository Prices { get; }

    public MintAuthorityRepository Authorities { get; }

    public WalletService WalletService { get; }

    public CurrencyService CurrencyService { get; }

    public CurrencyOptions Options { get; }

    public IInstrumentedCache Cache { get; }

    public ChargeHoldStore Holds { get; }

    public ChargeService ChargeService { get; }

    public MintService MintService { get; }

    public Mock<IAuditLogService> AuditLog { get; }

    public Mock<IAuditLogBuilder> AuditLogBuilder { get; }

    public Mock<IGuildMemberService> Members { get; }

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

    /// <summary>Prices a feature directly, bypassing the service's validation.</summary>
    public async Task<PriceEntry> SeedPriceAsync(
        Guid currencyId,
        string featureKey,
        long amount,
        ulong? guildId = 1001UL,
        bool isActive = true,
        params ulong[] exemptRoleIds)
    {
        var entry = new PriceEntry
        {
            Id = Guid.NewGuid(),
            FeatureKey = featureKey,
            GuildId = guildId,
            CurrencyId = currencyId,
            Amount = amount,
            ExemptRoleIds = exemptRoleIds.ToList(),
            IsActive = isActive,
            UpdatedById = 9000UL,
            UpdatedAt = DateTime.UtcNow
        };

        Db.PriceEntries.Add(entry);
        await Db.SaveChangesAsync();
        return entry;
    }

    /// <summary>Adds a mint authority directly, the way currency creation does for its creator.</summary>
    public async Task<MintAuthority> SeedMintAuthorityAsync(
        Guid currencyId,
        MintPrincipalType principalType,
        ulong? principalId)
    {
        var authority = new MintAuthority
        {
            Id = Guid.NewGuid(),
            CurrencyId = currencyId,
            PrincipalType = principalType,
            PrincipalId = principalId,
            GrantedById = 9000UL,
            GrantedAt = DateTime.UtcNow
        };

        Db.MintAuthorities.Add(authority);
        await Db.SaveChangesAsync();
        return authority;
    }

    /// <summary>Gives a user roles in a guild, as the synced member record would.</summary>
    public void SetMemberRoles(ulong guildId, ulong userId, params ulong[] roleIds)
    {
        Members
            .Setup(m => m.GetMemberAsync(guildId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildMemberDto { UserId = userId, RoleIds = roleIds.ToList() });
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        Db.Dispose();
        _connection.Dispose();
    }

    /// <summary>A fluent audit builder that returns itself, so a service can chain freely.</summary>
    private static Mock<IAuditLogBuilder> CreateAuditLogBuilderMock()
    {
        var builder = new Mock<IAuditLogBuilder>();
        builder.Setup(b => b.ForCategory(It.IsAny<AuditLogCategory>())).Returns(builder.Object);
        builder.Setup(b => b.WithAction(It.IsAny<AuditLogAction>())).Returns(builder.Object);
        builder.Setup(b => b.ByUser(It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(b => b.BySystem()).Returns(builder.Object);
        builder.Setup(b => b.ByBot()).Returns(builder.Object);
        builder.Setup(b => b.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(b => b.InGuild(It.IsAny<ulong>())).Returns(builder.Object);
        builder.Setup(b => b.WithDetails(It.IsAny<Dictionary<string, object?>>())).Returns(builder.Object);
        builder.Setup(b => b.WithDetails(It.IsAny<object>())).Returns(builder.Object);
        builder.Setup(b => b.FromIpAddress(It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(b => b.WithCorrelationId(It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(b => b.LogAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return builder;
    }
}
