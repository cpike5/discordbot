using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for NotXGuildSettings entities with settings-specific operations.
/// </summary>
public interface INotXGuildSettingsRepository : IRepository<NotXGuildSettings>
{
    /// <summary>
    /// Gets the not-X settings for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The settings for the guild, or null if not found.</returns>
    Task<NotXGuildSettings?> GetByGuildIdAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Gets the not-X settings for a guild, creating default settings if they don't exist.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The settings for the guild (existing or newly created with defaults).</returns>
    Task<NotXGuildSettings> GetOrCreateAsync(ulong guildId, CancellationToken ct = default);
}
