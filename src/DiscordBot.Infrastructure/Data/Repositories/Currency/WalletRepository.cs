using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for <see cref="Wallet"/>. Balances move only through
/// <see cref="ILedgerRepository"/>; nothing here writes <see cref="Wallet.CachedBalance"/>.
/// </summary>
public class WalletRepository : Repository<Wallet>, IWalletRepository
{
    private readonly ILogger<WalletRepository> _logger;

    public WalletRepository(
        BotDbContext context,
        ILogger<WalletRepository> logger,
        ILogger<Repository<Wallet>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Wallet?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(w => w.Currency)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Wallet?> GetAsync(Guid currencyId, ulong userId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(w => w.Currency)
            .FirstOrDefaultAsync(w => w.CurrencyId == currencyId && w.UserId == userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Wallet> GetOrCreateAsync(Guid currencyId, ulong userId, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(currencyId, userId, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            CurrencyId = currencyId,
            UserId = userId,
            CachedBalance = 0,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await DbSet.AddAsync(wallet, cancellationToken);
            await Context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Created wallet {WalletId} for user {UserId} in currency {CurrencyId}", wallet.Id, userId, currencyId);
            return wallet;
        }
        catch (DbUpdateException)
        {
            // Lost the race on IX_Wallets_CurrencyId_UserId. Drop our attempt and take theirs.
            Context.Entry(wallet).State = EntityState.Detached;

            var raced = await GetAsync(currencyId, userId, cancellationToken);
            if (raced != null)
            {
                return raced;
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Wallet>> GetForUserAsync(
        ulong userId,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Include(w => w.Currency)
            .Where(w => w.UserId == userId);

        if (guildId.HasValue)
        {
            var guild = guildId.Value;
            query = query.Where(w => w.Currency!.Scope == CurrencyScope.Global || w.Currency.GuildId == guild);
        }

        return await query
            .OrderBy(w => w.Currency!.Scope)
            .ThenBy(w => w.Currency!.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Wallet>> GetForCurrencyAsync(
        Guid currencyId,
        bool debtorsOnly = false,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().Where(w => w.CurrencyId == currencyId);

        if (debtorsOnly)
        {
            query = query.Where(w => w.CachedBalance < 0);
        }

        return await query
            .OrderByDescending(w => w.CachedBalance)
            .ThenBy(w => w.UserId)
            .ToListAsync(cancellationToken);
    }
}
