using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Data access for <see cref="Wallet"/>. Balances are only ever moved by the ledger, never by
/// writing <see cref="Wallet.CachedBalance"/> through this repository.
/// </summary>
public interface IWalletRepository : IRepository<Wallet>
{
    /// <summary>Gets one wallet by id, or null.</summary>
    Task<Wallet?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Gets a user's wallet in one currency, or null when they have never held any.</summary>
    Task<Wallet?> GetAsync(Guid currencyId, ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user's wallet in one currency, creating an empty one when it does not exist. Safe to
    /// race: a concurrent create is caught on the unique index and the existing row is returned.
    /// </summary>
    Task<Wallet> GetOrCreateAsync(Guid currencyId, ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every wallet a user holds, optionally narrowed to the currencies visible in one guild.
    /// </summary>
    /// <param name="userId">Discord user snowflake ID.</param>
    /// <param name="guildId">Guild to narrow to, or null for every wallet the user holds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Wallet>> GetForUserAsync(
        ulong userId,
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the wallets in one currency, highest balance first.</summary>
    /// <param name="currencyId">Currency to list.</param>
    /// <param name="debtorsOnly">Whether to return only wallets below zero.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Wallet>> GetForCurrencyAsync(
        Guid currencyId,
        bool debtorsOnly = false,
        CancellationToken cancellationToken = default);
}
