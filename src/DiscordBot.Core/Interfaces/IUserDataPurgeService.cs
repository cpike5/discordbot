using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for purging user data from the system.
/// Provides methods for privacy compliance and "right to be forgotten" requests.
/// </summary>
public interface IUserDataPurgeService
{
    /// <summary>
    /// Retrieves a summary of all user data that would be affected by a purge operation.
    /// Use this method to preview what data will be deleted before executing the purge.
    /// </summary>
    /// <param name="discordUserId">The Discord user ID to analyze.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A summary containing counts of all data entities associated with the user.</returns>
    Task<UserDataSummary> GetUserDataSummaryAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently purges all user data from the system.
    /// This operation is irreversible and should be used for privacy compliance requests.
    /// Deletes the User entity and all related data across CommandLog, MessageLog, GuildMember,
    /// UserConsent, RatWatch, RatVote, RatRecord, Reminder, FlaggedEvent, ModerationCase,
    /// ModNote, UserModTag, Watchlist, and MemberActivitySnapshot entities.
    /// </summary>
    /// <param name="discordUserId">The Discord user ID whose data should be purged.</param>
    /// <param name="initiatedBy">Identifier of the actor initiating the purge (for audit logging).</param>
    /// <param name="reason">Optional reason for the purge operation.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure, along with counts of deleted records.</returns>
    Task<UserDataPurgeResult> PurgeUserDataAsync(
        ulong discordUserId,
        string initiatedBy,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
