using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Represents the automatic action to take when content is flagged.
/// </summary>
public enum AutoAction
{
    /// <summary>
    /// No automatic action - flag for review only.
    /// </summary>
    None = 0,

    /// <summary>
    /// Automatically delete the offending message.
    /// </summary>
    Delete = 1,

    /// <summary>
    /// Automatically warn the user (create a case).
    /// </summary>
    Warn = 2,

    /// <summary>
    /// Automatically mute/timeout the user.
    /// </summary>
    Mute = 3,

    /// <summary>
    /// Automatically kick the user from the guild.
    /// </summary>
    Kick = 4,

    /// <summary>
    /// Automatically ban the user from the guild.
    /// </summary>
    Ban = 5
}

/// <summary>
/// Represents the automatic action to take when a raid is detected.
/// </summary>
public enum RaidAutoAction
{
    /// <summary>
    /// No automatic action - alert moderators only.
    /// </summary>
    None = 0,

    /// <summary>
    /// Alert moderators but take no defensive action.
    /// </summary>
    AlertOnly = 1,

    /// <summary>
    /// Disable invites to prevent further joins.
    /// </summary>
    LockInvites = 2,

    /// <summary>
    /// Lock down the entire server (verification level up, permissions down).
    /// </summary>
    LockServer = 3
}

/// <summary>
/// Data transfer object representing a moderation case for display purposes.
/// Includes resolved usernames and formatted timestamps.
/// </summary>
public class ModerationCaseDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this moderation case.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the sequential case number within the guild.
    /// </summary>
    public int CaseNumber { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this case occurred.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the user being moderated.
    /// </summary>
    public ulong TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the target user (resolved from Discord).
    /// </summary>
    public string TargetUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator who created this case.
    /// </summary>
    public ulong ModeratorUserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the moderator (resolved from Discord).
    /// </summary>
    public string ModeratorUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of moderation action taken.
    /// </summary>
    public CaseType Type { get; set; }

    /// <summary>
    /// Gets or sets the reason for the moderation action.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the duration of the punishment (for temporary bans/mutes).
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this case was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this case expires (UTC, for temporary punishments).
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the ID of the related flagged event that triggered this case.
    /// </summary>
    public Guid? RelatedFlaggedEventId { get; set; }

    /// <summary>
    /// Gets or sets the Discord message snowflake ID that triggered this case.
    /// </summary>
    public ulong? ContextMessageId { get; set; }

    /// <summary>
    /// Gets or sets the Discord channel snowflake ID where the context message was posted.
    /// </summary>
    public ulong? ContextChannelId { get; set; }

    /// <summary>
    /// Gets or sets the cached content of the message that triggered this case.
    /// </summary>
    public string? ContextMessageContent { get; set; }
}

/// <summary>
/// Data transfer object for creating a new moderation case.
/// </summary>
public class ModerationCaseCreateDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this case will be created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the user being moderated.
    /// </summary>
    public ulong TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator creating this case.
    /// </summary>
    public ulong ModeratorUserId { get; set; }

    /// <summary>
    /// Gets or sets the type of moderation action to take.
    /// </summary>
    public CaseType Type { get; set; }

    /// <summary>
    /// Gets or sets the optional reason for the moderation action.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the optional duration of the punishment (for temporary bans/mutes).
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Gets or sets the optional ID of the related flagged event that triggered this case.
    /// </summary>
    public Guid? RelatedFlaggedEventId { get; set; }

    /// <summary>
    /// Gets or sets the optional Discord message snowflake ID that triggered this case.
    /// </summary>
    public ulong? ContextMessageId { get; set; }

    /// <summary>
    /// Gets or sets the optional Discord channel snowflake ID where the context message was posted.
    /// </summary>
    public ulong? ContextChannelId { get; set; }

    /// <summary>
    /// Gets or sets the optional cached content of the message that triggered this case.
    /// </summary>
    public string? ContextMessageContent { get; set; }
}

/// <summary>
/// Data transfer object for querying moderation cases with filters and pagination.
/// </summary>
public class ModerationCaseQueryDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID to filter by.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the optional case type to filter by.
    /// </summary>
    public CaseType? Type { get; set; }

    /// <summary>
    /// Gets or sets the optional target user ID to filter by.
    /// </summary>
    public ulong? TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the optional moderator user ID to filter by.
    /// </summary>
    public ulong? ModeratorUserId { get; set; }

    /// <summary>
    /// Gets or sets the optional start date for filtering cases (UTC).
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the optional end date for filtering cases (UTC).
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the page number for pagination (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size for pagination.
    /// </summary>
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// DTO for updating a case reason.
/// </summary>
public class CaseReasonUpdateDto
{
    /// <summary>
    /// Gets or sets the new reason.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator making the update.
    /// </summary>
    public ulong ModeratorId { get; set; }
}
