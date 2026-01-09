using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing Discord guild memberships captured during OAuth authentication.
/// Stores guild membership data locally to enable guild-based access control without
/// requiring real-time Discord API calls.
/// </summary>
public interface IUserDiscordGuildService
{
    /// <summary>
    /// Stores or updates guild memberships for a user from OAuth data.
    /// Handles both new memberships and updates to existing ones.
    /// Removes memberships for guilds the user has left.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="guilds">Collection of guild DTOs from Discord OAuth.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of guild memberships stored/updated.</returns>
    Task<int> StoreGuildMembershipsAsync(
        string applicationUserId,
        IEnumerable<DiscordGuildDto> guilds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all stored guild memberships for a user.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of the user's guild memberships.</returns>
    Task<IReadOnlyList<UserDiscordGuild>> GetUserGuildsAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has stored membership in a specific guild.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has stored membership in the guild.</returns>
    Task<bool> HasGuildMembershipAsync(
        string applicationUserId,
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all stored guild memberships for a user.
    /// Called when a user unlinks their Discord account.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteUserGuildsAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default);
}
