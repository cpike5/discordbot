using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Data access for <see cref="PriceEntry"/>. A feature with no active entry is free.
/// </summary>
public interface IPriceRepository : IRepository<PriceEntry>
{
    /// <summary>Gets one entry by id, or null.</summary>
    Task<PriceEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active price for a feature in a guild, or null when the feature is free there.
    /// Looks for the guild's own entry first, then the entry that applies everywhere.
    /// </summary>
    Task<PriceEntry?> GetActiveAsync(string featureKey, ulong? guildId, CancellationToken cancellationToken = default);

    /// <summary>Gets the entry for an exact (feature key, guild) pair, active or not.</summary>
    Task<PriceEntry?> GetByFeatureAsync(string featureKey, ulong? guildId, CancellationToken cancellationToken = default);

    /// <summary>Gets every entry that applies in a guild, including the global ones.</summary>
    Task<IReadOnlyList<PriceEntry>> GetForGuildAsync(ulong guildId, CancellationToken cancellationToken = default);
}
