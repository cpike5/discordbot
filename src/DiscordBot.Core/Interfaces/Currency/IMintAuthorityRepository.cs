using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Data access for <see cref="MintAuthority"/>, the list of principals allowed to create units of a
/// currency.
/// </summary>
public interface IMintAuthorityRepository : IRepository<MintAuthority>
{
    /// <summary>Gets one grant by id, or null.</summary>
    Task<MintAuthority?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets every grant on a currency, oldest first.</summary>
    Task<IReadOnlyList<MintAuthority>> GetForCurrencyAsync(Guid currencyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a principal may mint. A null <paramref name="userId"/> means the system principal,
    /// which matches only a <c>System</c> grant.
    /// </summary>
    /// <param name="currencyId">Currency to check.</param>
    /// <param name="userId">Discord user snowflake ID, or null for the system principal.</param>
    /// <param name="userRoleIds">The user's Discord role snowflake IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> HasAuthorityAsync(
        Guid currencyId,
        ulong? userId,
        IReadOnlyCollection<ulong>? userRoleIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>Whether an identical grant already exists on the currency.</summary>
    Task<bool> ExistsAsync(
        Guid currencyId,
        Enums.MintPrincipalType principalType,
        ulong? principalId,
        CancellationToken cancellationToken = default);
}
