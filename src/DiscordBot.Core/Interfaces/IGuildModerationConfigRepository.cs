using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for GuildModerationConfig entities with configuration-specific operations.
/// </summary>
public interface IGuildModerationConfigRepository : IRepository<GuildModerationConfig>
{
    /// <summary>
    /// Gets the moderation configuration for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild moderation configuration, or null if not found.</returns>
    Task<GuildModerationConfig?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);
}
