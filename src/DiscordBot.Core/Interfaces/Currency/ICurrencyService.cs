using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Currency administration: creating and editing currencies, managing who may mint them, and
/// pricing features.
/// <para>
/// This service enforces the rules that belong to the data (name uniqueness, a debt floor when debt
/// is allowed, a guild currency only pricing its own guild). Who is <em>allowed</em> to call it is
/// the caller's business: the Discord preconditions and portal authorization handlers decide that.
/// </para>
/// </summary>
public interface ICurrencyService
{
    /// <summary>Gets one currency, or null.</summary>
    Task<CurrencyDto?> GetAsync(Guid currencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currencies usable in a guild: that guild's own plus every global one.
    /// </summary>
    Task<IReadOnlyList<CurrencyDto>> GetVisibleInGuildAsync(
        ulong guildId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the bot-wide currencies.</summary>
    Task<IReadOnlyList<CurrencyDto>> GetGlobalAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a currency and adds its creator as a <see cref="MintPrincipalType.User"/> mint
    /// authority.
    /// </summary>
    Task<CurrencyResult> CreateAsync(CurrencyCreateDto request, ulong createdById, CancellationToken cancellationToken = default);

    /// <summary>Changes a currency's rules. Scope, guild, and creator are fixed.</summary>
    Task<CurrencyResult> UpdateAsync(Guid currencyId, CurrencyUpdateDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Freezes a currency. No mint, spend, transfer, or fine afterwards; history stays readable.
    /// Currencies are never deleted.
    /// </summary>
    Task<CurrencyResult> DeactivateAsync(Guid currencyId, CancellationToken cancellationToken = default);

    /// <summary>Gets the principals allowed to mint a currency.</summary>
    Task<IReadOnlyList<MintAuthorityDto>> GetMintAuthoritiesAsync(Guid currencyId, CancellationToken cancellationToken = default);

    /// <summary>Adds a principal to a currency's mint authority list.</summary>
    Task<MintAuthorityResult> GrantMintAuthorityAsync(
        Guid currencyId,
        MintPrincipalType principalType,
        ulong? principalId,
        ulong grantedById,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a grant. Returns false when it no longer exists.</summary>
    Task<bool> RevokeMintAuthorityAsync(Guid authorityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the price of a feature, replacing any existing entry for the same (feature key, guild)
    /// pair. A guild currency may only price features in its own guild.
    /// </summary>
    Task<PriceEntryResult> SetPriceAsync(PriceEntrySaveDto request, ulong updatedById, CancellationToken cancellationToken = default);

    /// <summary>Deactivates a price, leaving the feature free. Returns false when there was none.</summary>
    Task<bool> RemovePriceAsync(string featureKey, ulong? guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active price for a feature in a guild, or null when the feature is free there.
    /// </summary>
    Task<PriceEntryDto?> GetActivePriceAsync(string featureKey, ulong? guildId, CancellationToken cancellationToken = default);

    /// <summary>Gets every price that applies in a guild.</summary>
    Task<IReadOnlyList<PriceEntryDto>> GetPricesForGuildAsync(ulong guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares every wallet's cached balance against the sum of its ledger rows and returns only
    /// the wallets that disagree. An empty list means the currency reconciles.
    /// </summary>
    Task<IReadOnlyList<WalletReconciliationDto>> ReconcileAsync(Guid currencyId, CancellationToken cancellationToken = default);
}
