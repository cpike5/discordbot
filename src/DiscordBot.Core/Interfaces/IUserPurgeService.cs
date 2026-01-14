using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for purging user data from the system (GDPR right to be forgotten).
/// </summary>
public interface IUserPurgeService
{
    /// <summary>
    /// Purges all user data from the system.
    /// </summary>
    Task<UserPurgeResultDto> PurgeUserDataAsync(
        ulong discordUserId,
        PurgeInitiator initiator,
        string? initiatorId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a preview of data that would be deleted without actually deleting.
    /// </summary>
    Task<UserPurgeResultDto> PreviewPurgeAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if user can be purged (no active admin roles, etc.)
    /// </summary>
    Task<(bool CanPurge, string? BlockingReason)> CanPurgeUserAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);
}
