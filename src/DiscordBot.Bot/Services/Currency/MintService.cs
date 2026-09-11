using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Currency;

/// <summary>
/// The authority check in front of minting. Mint is the only source of units, so it is the only
/// operation with its own allow list; the balance half stays in <see cref="IWalletService"/>.
/// </summary>
public class MintService : IMintService
{
    private readonly ICurrencyRepository _currencies;
    private readonly IMintAuthorityRepository _authorities;
    private readonly IWalletService _wallets;
    private readonly IGuildMemberService _members;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<MintService> _logger;

    public MintService(
        ICurrencyRepository currencies,
        IMintAuthorityRepository authorities,
        IWalletService wallets,
        IGuildMemberService members,
        IAuditLogService auditLog,
        ILogger<MintService> logger)
    {
        _currencies = currencies;
        _authorities = authorities;
        _wallets = wallets;
        _members = members;
        _auditLog = auditLog;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MintResult> MintAsync(
        Guid currencyId,
        ulong toUserId,
        long amount,
        string reason,
        LedgerSource source,
        string idempotencyKey,
        ulong? actorId,
        CancellationToken cancellationToken = default)
    {
        var currency = await _currencies.GetAsync(currencyId, cancellationToken);
        if (currency == null)
        {
            return MintResult.Failed(CurrencyErrors.CurrencyNotFound);
        }

        // A null actor is the system principal, which mints only when the currency carries a
        // System grant. That is the hook a future income job uses instead of a fake user.
        var roleIds = actorId.HasValue
            ? await GetGuildRoleIdsAsync(currency.GuildId, actorId.Value, cancellationToken)
            : Array.Empty<ulong>();

        if (!await _authorities.HasAuthorityAsync(currencyId, actorId, roleIds, cancellationToken))
        {
            _logger.LogWarning(
                "Mint of currency {CurrencyId} refused: actor {ActorId} is not a mint authority",
                currencyId, actorId?.ToString() ?? "system");
            return MintResult.Failed(CurrencyErrors.NotAuthorizedToMint);
        }

        var result = await _wallets.MintAsync(
            currencyId, toUserId, amount, reason, source, idempotencyKey, actorId, cancellationToken);

        if (!result.Success || result.WasDuplicate)
        {
            return result;
        }

        var builder = _auditLog.CreateBuilder()
            .ForCategory(AuditLogCategory.User)
            .WithAction(AuditLogAction.CurrencyMinted)
            .OnTarget("Wallet", toUserId.ToString())
            .WithDetails(new
            {
                currencyId,
                currencyName = currency.Name,
                toUserId = toUserId.ToString(),
                amount,
                source = source.ToString(),
                reason
            });

        builder = actorId.HasValue ? builder.ByUser(actorId.Value.ToString()) : builder.BySystem();

        if (currency.GuildId.HasValue)
        {
            builder = builder.InGuild(currency.GuildId.Value);
        }

        await builder.LogAsync(cancellationToken);

        return result;
    }

    /// <inheritdoc />
    public Task<bool> CanMintAsync(
        Guid currencyId,
        ulong userId,
        IReadOnlyCollection<ulong> userRoleIds,
        CancellationToken cancellationToken = default) =>
        _authorities.HasAuthorityAsync(currencyId, userId, userRoleIds, cancellationToken);

    /// <summary>
    /// The actor's roles in the currency's guild, for a role grant. A global currency has no guild
    /// and therefore no role authorities, only user and system ones.
    /// </summary>
    private async Task<IReadOnlyCollection<ulong>> GetGuildRoleIdsAsync(
        ulong? guildId,
        ulong userId,
        CancellationToken cancellationToken)
    {
        if (!guildId.HasValue)
        {
            return Array.Empty<ulong>();
        }

        try
        {
            var member = await _members.GetMemberAsync(guildId.Value, userId, cancellationToken);
            return member?.RoleIds ?? (IReadOnlyCollection<ulong>)Array.Empty<ulong>();
        }
        catch (Exception ex)
        {
            // Without roles the check falls back to the user's own grant, which refuses rather
            // than lets an unauthorized mint through.
            _logger.LogError(ex, "Could not read roles for user {UserId} in guild {GuildId}", userId, guildId);
            return Array.Empty<ulong>();
        }
    }
}
