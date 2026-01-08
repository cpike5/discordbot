namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a user data purge operation.
/// Contains success/failure status and detailed counts of deleted records.
/// </summary>
public class UserDataPurgeResult
{
    /// <summary>
    /// Indicates whether the purge operation succeeded.
    /// </summary>
    public bool Succeeded { get; private set; }

    /// <summary>
    /// Error code if the operation failed.
    /// </summary>
    public string? ErrorCode { get; private set; }

    /// <summary>
    /// Error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// The Discord user ID that was purged.
    /// </summary>
    public ulong? DiscordUserId { get; private set; }

    /// <summary>
    /// Detailed breakdown of deleted records by entity type.
    /// </summary>
    public UserDataPurgeCounts? DeletedCounts { get; private set; }

    /// <summary>
    /// Timestamp when the purge operation completed (UTC).
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// Creates a successful purge result.
    /// </summary>
    /// <param name="discordUserId">The Discord user ID that was purged.</param>
    /// <param name="counts">Detailed counts of deleted records.</param>
    /// <returns>A successful purge result.</returns>
    public static UserDataPurgeResult Success(ulong discordUserId, UserDataPurgeCounts counts) => new()
    {
        Succeeded = true,
        DiscordUserId = discordUserId,
        DeletedCounts = counts,
        CompletedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Creates a failed purge result.
    /// </summary>
    /// <param name="errorCode">Error code identifying the failure type.</param>
    /// <param name="errorMessage">Human-readable error message.</param>
    /// <returns>A failed purge result.</returns>
    public static UserDataPurgeResult Failure(string errorCode, string errorMessage) => new()
    {
        Succeeded = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    // Common error codes
    /// <summary>
    /// Error code: User not found in the database.
    /// </summary>
    public const string UserNotFound = "USER_NOT_FOUND";

    /// <summary>
    /// Error code: Database error occurred during purge operation.
    /// </summary>
    public const string DatabaseError = "DATABASE_ERROR";

    /// <summary>
    /// Error code: Insufficient permissions to perform purge operation.
    /// </summary>
    public const string InsufficientPermissions = "INSUFFICIENT_PERMISSIONS";

    /// <summary>
    /// Error code: Operation was cancelled.
    /// </summary>
    public const string OperationCancelled = "OPERATION_CANCELLED";
}

/// <summary>
/// Summary of user data that exists in the system.
/// Used to preview what would be deleted in a purge operation.
/// </summary>
public record UserDataSummary
{
    /// <summary>
    /// The Discord user ID being analyzed.
    /// </summary>
    public ulong DiscordUserId { get; init; }

    /// <summary>
    /// Indicates whether a User entity exists for this Discord user ID.
    /// </summary>
    public bool UserExists { get; init; }

    /// <summary>
    /// Breakdown of data counts by entity type.
    /// </summary>
    public UserDataPurgeCounts Counts { get; init; } = new();

    /// <summary>
    /// Total count of all records that would be affected by a purge.
    /// </summary>
    public int TotalRecords => Counts.TotalRecords;
}

/// <summary>
/// Detailed breakdown of user data counts by entity type.
/// </summary>
public record UserDataPurgeCounts
{
    /// <summary>
    /// Number of CommandLog records (where UserId matches).
    /// </summary>
    public int CommandLogs { get; init; }

    /// <summary>
    /// Number of MessageLog records (where AuthorId matches).
    /// </summary>
    public int MessageLogs { get; init; }

    /// <summary>
    /// Number of GuildMember records (where UserId matches).
    /// </summary>
    public int GuildMembers { get; init; }

    /// <summary>
    /// Number of UserConsent records (where DiscordUserId matches).
    /// </summary>
    public int UserConsents { get; init; }

    /// <summary>
    /// Number of RatWatch records (where AccusedUserId or InitiatorUserId matches).
    /// </summary>
    public int RatWatches { get; init; }

    /// <summary>
    /// Number of RatVote records (where VoterUserId matches).
    /// </summary>
    public int RatVotes { get; init; }

    /// <summary>
    /// Number of RatRecord records (where UserId matches).
    /// </summary>
    public int RatRecords { get; init; }

    /// <summary>
    /// Number of Reminder records (where UserId matches).
    /// </summary>
    public int Reminders { get; init; }

    /// <summary>
    /// Number of FlaggedEvent records (where UserId or ReviewedByUserId matches).
    /// </summary>
    public int FlaggedEvents { get; init; }

    /// <summary>
    /// Number of ModerationCase records (where TargetUserId or ModeratorUserId matches).
    /// </summary>
    public int ModerationCases { get; init; }

    /// <summary>
    /// Number of ModNote records (where TargetUserId or AuthorUserId matches).
    /// </summary>
    public int ModNotes { get; init; }

    /// <summary>
    /// Number of UserModTag records (where UserId or AppliedByUserId matches).
    /// </summary>
    public int UserModTags { get; init; }

    /// <summary>
    /// Number of Watchlist records (where UserId or AddedByUserId matches).
    /// </summary>
    public int Watchlists { get; init; }

    /// <summary>
    /// Number of MemberActivitySnapshot records (where UserId matches).
    /// </summary>
    public int MemberActivitySnapshots { get; init; }

    /// <summary>
    /// Number of User records (should be 0 or 1).
    /// </summary>
    public int Users { get; init; }

    /// <summary>
    /// Total count of all records across all entity types.
    /// </summary>
    public int TotalRecords =>
        CommandLogs +
        MessageLogs +
        GuildMembers +
        UserConsents +
        RatWatches +
        RatVotes +
        RatRecords +
        Reminders +
        FlaggedEvents +
        ModerationCases +
        ModNotes +
        UserModTags +
        Watchlists +
        MemberActivitySnapshots +
        Users;
}
