using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for guild operations and settings management.
/// </summary>
public interface IGuildService
{
    /// <summary>
    /// Gets all guilds with merged database and Discord data.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of guild data.</returns>
    Task<IReadOnlyList<GuildDto>> GetAllGuildsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets guilds with filtering, sorting, and pagination support.
    /// </summary>
    /// <param name="query">The search query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated response of guild data.</returns>
    Task<PaginatedResponseDto<GuildDto>> GetGuildsAsync(
        GuildSearchQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific guild by ID with merged database and Discord data.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild data, or null if not found.</returns>
    Task<GuildDto?> GetGuildByIdAsync(ulong guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates guild settings.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The update request containing the fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated guild data, or null if the guild was not found.</returns>
    Task<GuildDto?> UpdateGuildAsync(ulong guildId, GuildUpdateRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes guild data from Discord to the database.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the sync was successful, false if the guild was not found.</returns>
    Task<bool> SyncGuildAsync(ulong guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes all connected guilds from Discord to the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of guilds successfully synced.</returns>
    Task<int> SyncAllGuildsAsync(CancellationToken cancellationToken = default);
}
