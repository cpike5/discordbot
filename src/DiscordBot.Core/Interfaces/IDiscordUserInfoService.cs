using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for retrieving Discord user information via the Discord API.
/// Provides caching to minimize API calls.
/// </summary>
public interface IDiscordUserInfoService
{
    /// <summary>
    /// Retrieves Discord user information for an ApplicationUser.
    /// Results are cached to reduce API calls.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="forceRefresh">If true, bypasses cache and fetches fresh data from Discord API.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Discord user info, or null if the user has no valid OAuth token.</returns>
    Task<DiscordUserInfoDto?> GetUserInfoAsync(
        string applicationUserId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the list of guilds (servers) the user is a member of.
    /// Results are cached to reduce API calls.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="forceRefresh">If true, bypasses cache and fetches fresh data from Discord API.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of guilds the user is in.</returns>
    Task<IReadOnlyList<DiscordGuildDto>> GetUserGuildsAsync(
        string applicationUserId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cache for a specific user.
    /// Called when tokens are refreshed or user data needs to be reloaded.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    void InvalidateCache(string applicationUserId);
}
