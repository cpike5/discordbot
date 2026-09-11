using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for <see cref="MintAuthority"/>.
/// </summary>
public class MintAuthorityRepository : Repository<MintAuthority>, IMintAuthorityRepository
{
    private readonly ILogger<MintAuthorityRepository> _logger;

    public MintAuthorityRepository(
        BotDbContext context,
        ILogger<MintAuthorityRepository> logger,
        ILogger<Repository<MintAuthority>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MintAuthority?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MintAuthority>> GetForCurrencyAsync(Guid currencyId, CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking()
            .Where(a => a.CurrencyId == currencyId)
            .OrderBy(a => a.GrantedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasAuthorityAsync(
        Guid currencyId,
        ulong? userId,
        IReadOnlyCollection<ulong>? userRoleIds = null,
        CancellationToken cancellationToken = default)
    {
        var grants = await DbSet.AsNoTracking()
            .Where(a => a.CurrencyId == currencyId)
            .ToListAsync(cancellationToken);

        if (grants.Count == 0)
        {
            return false;
        }

        // No user means the system principal: a background job minting on its own behalf.
        if (userId == null)
        {
            return grants.Any(a => a.PrincipalType == MintPrincipalType.System);
        }

        if (grants.Any(a => a.PrincipalType == MintPrincipalType.User && a.PrincipalId == userId))
        {
            return true;
        }

        if (userRoleIds is { Count: > 0 } &&
            grants.Any(a => a.PrincipalType == MintPrincipalType.Role && a.PrincipalId.HasValue && userRoleIds.Contains(a.PrincipalId.Value)))
        {
            return true;
        }

        _logger.LogDebug("User {UserId} holds no mint authority on currency {CurrencyId}", userId, currencyId);
        return false;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        Guid currencyId,
        MintPrincipalType principalType,
        ulong? principalId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.AsNoTracking().AnyAsync(
            a => a.CurrencyId == currencyId && a.PrincipalType == principalType && a.PrincipalId == principalId,
            cancellationToken);
    }
}
