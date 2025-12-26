using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for WelcomeConfiguration entities with Discord-specific operations.
/// </summary>
public interface IWelcomeConfigurationRepository : IRepository<WelcomeConfiguration>
{
    /// <summary>
    /// Gets a welcome configuration by its guild ID.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The welcome configuration for the specified guild, or null if not found.</returns>
    Task<WelcomeConfiguration?> GetByGuildIdAsync(ulong guildId, CancellationToken cancellationToken = default);
}
