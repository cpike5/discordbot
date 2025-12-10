using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for checking Discord guild membership and administrative permissions.
/// Used by authorization policies to verify user access to guild-specific resources.
/// </summary>
public interface IGuildMembershipService
{
    /// <summary>
    /// Checks if a user is a member of the specified guild.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="guildId">The Discord guild ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user is a member of the guild, false otherwise.</returns>
    Task<bool> IsMemberOfGuildAsync(
        string applicationUserId,
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has administrative permissions in the specified guild.
    /// Administrative permissions include: guild owner, Administrator permission, or Manage Guild permission.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="guildId">The Discord guild ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has admin permissions in the guild, false otherwise.</returns>
    Task<bool> IsGuildAdminAsync(
        string applicationUserId,
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the list of guilds where the user has administrative permissions.
    /// Used to populate guild selection dropdowns for admin users.
    /// </summary>
    /// <param name="applicationUserId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of guilds where the user has admin permissions.</returns>
    Task<IReadOnlyList<DiscordGuildDto>> GetAdministeredGuildsAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default);
}
