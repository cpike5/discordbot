using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Data access for <see cref="Currency"/>. Currencies are never deleted, only deactivated.
/// </summary>
public interface ICurrencyRepository : IRepository<Currency>
{
    /// <summary>Gets one currency by id, or null.</summary>
    Task<Currency?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the currencies usable in a guild: that guild's own currencies plus every global one.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="includeInactive">Whether to include deactivated currencies.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<Currency>> GetVisibleInGuildAsync(
        ulong guildId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the currencies owned by one guild.</summary>
    Task<IReadOnlyList<Currency>> GetForGuildAsync(
        ulong guildId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the bot-wide currencies.</summary>
    Task<IReadOnlyList<Currency>> GetGlobalAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a currency with this name already exists in the same scope. Names are compared
    /// case-insensitively.
    /// </summary>
    /// <param name="scope">Scope to search.</param>
    /// <param name="guildId">Owning guild for a guild scope, null for global.</param>
    /// <param name="name">Name to look for.</param>
    /// <param name="excludeId">Currency to ignore, so an edit does not clash with itself.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<bool> NameExistsAsync(
        CurrencyScope scope,
        ulong? guildId,
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
}
