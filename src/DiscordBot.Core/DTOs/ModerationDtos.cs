using System.Text.Json.Serialization;
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
/// Data transfer object for moderator statistics summary.
/// Includes aggregated action counts and top moderators list.
/// </summary>
public class ModeratorStatsSummaryDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID for these statistics.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the optional specific moderator ID these stats are for.
    /// Null indicates guild-wide statistics.
    /// </summary>
    public ulong? ModeratorId { get; set; }

    /// <summary>
    /// Gets or sets the optional moderator username (resolved from Discord).
    /// </summary>
    public string? ModeratorUsername { get; set; }

    /// <summary>
    /// Gets or sets the start date of the statistics period (UTC).
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date of the statistics period (UTC).
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the total number of moderation cases in the period.
    /// </summary>
    public int TotalCases { get; set; }

    /// <summary>
    /// Gets or sets the number of warnings issued in the period.
    /// </summary>
    public int WarnCount { get; set; }

    /// <summary>
    /// Gets or sets the number of kicks issued in the period.
    /// </summary>
    public int KickCount { get; set; }

    /// <summary>
    /// Gets or sets the number of bans issued in the period.
    /// </summary>
    public int BanCount { get; set; }

    /// <summary>
    /// Gets or sets the number of mutes issued in the period.
    /// </summary>
    public int MuteCount { get; set; }

    /// <summary>
    /// Gets or sets the list of top moderators by action count.
    /// Empty if this is a single-moderator summary.
    /// </summary>
    public IReadOnlyList<ModeratorStatsEntryDto> TopModerators { get; set; } = Array.Empty<ModeratorStatsEntryDto>();
}

/// <summary>
/// Data transfer object for individual moderator statistics entry.
/// </summary>
public class ModeratorStatsEntryDto
{
    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the moderator (resolved from Discord).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of moderation actions performed.
    /// </summary>
    public int TotalActions { get; set; }

    /// <summary>
    /// Gets or sets the number of warnings issued.
    /// </summary>
    public int WarnCount { get; set; }

    /// <summary>
    /// Gets or sets the number of kicks issued.
    /// </summary>
    public int KickCount { get; set; }

    /// <summary>
    /// Gets or sets the number of bans issued.
    /// </summary>
    public int BanCount { get; set; }

    /// <summary>
    /// Gets or sets the number of mutes issued.
    /// </summary>
    public int MuteCount { get; set; }
}

/// <summary>
/// Data transfer object for mod note information.
/// </summary>
public class ModNoteDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this note.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this note exists.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the user this note is about.
    /// </summary>
    public ulong TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the target user (resolved from Discord).
    /// </summary>
    public string TargetUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator who created this note.
    /// </summary>
    public ulong AuthorUserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the note author (resolved from Discord).
    /// </summary>
    public string AuthorUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the note.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this note was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Data transfer object for creating a new moderator note.
/// </summary>
public class ModNoteCreateDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this note will be created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the user this note is about.
    /// </summary>
    public ulong TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator creating this note.
    /// </summary>
    public ulong AuthorUserId { get; set; }

    /// <summary>
    /// Gets or sets the content of the note.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Data transfer object for creating a new mod tag.
/// </summary>
public class ModTagCreateDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this tag will be created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the name of the tag.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the color of the tag (hex format, e.g., "#FF5733").
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the tag.
    /// </summary>
    public TagCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the optional description of the tag.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Data transfer object for mod tag information.
/// </summary>
public class ModTagDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this tag.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this tag exists.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the name of the tag.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the color of the tag (hex format).
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the tag.
    /// </summary>
    public TagCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the optional description of the tag.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this tag was created from a template.
    /// </summary>
    public bool IsFromTemplate { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this tag was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the number of users currently assigned this tag.
    /// </summary>
    public int UserCount { get; set; }
}

/// <summary>
/// Data transfer object for user mod tag association.
/// </summary>
public class UserModTagDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this association.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the tagged user.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the tagged user (resolved from Discord).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tag ID.
    /// </summary>
    public Guid TagId { get; set; }

    /// <summary>
    /// Gets or sets the name of the tag applied to the user.
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the color of the tag in hex format (e.g., "#FF5733").
    /// </summary>
    public string TagColor { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category/sentiment of the tag.
    /// </summary>
    public TagCategory TagCategory { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of who applied this tag.
    /// </summary>
    public ulong AppliedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the username of who applied the tag (resolved from Discord).
    /// </summary>
    public string AppliedByUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this tag was applied (UTC).
    /// </summary>
    public DateTime AppliedAt { get; set; }
}

/// <summary>
/// Data transfer object for watchlist entry information.
/// </summary>
public class WatchlistEntryDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this watchlist entry.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the watched user.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the watched user (resolved from Discord).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of who added this user to the watchlist.
    /// </summary>
    public ulong AddedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the username of who added this entry (resolved from Discord).
    /// </summary>
    public string AddedByUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional reason for adding to the watchlist.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this user was added to the watchlist (UTC).
    /// </summary>
    public DateTime AddedAt { get; set; }
}

/// <summary>
/// Data transfer object for adding a user to the watchlist.
/// </summary>
public class WatchlistAddDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this entry will be created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the user to add to the watchlist.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator adding this entry.
    /// </summary>
    public ulong AddedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the optional reason why this user is being added to the watchlist.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Data transfer object for flagged event information.
/// </summary>
public class FlaggedEventDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this flagged event.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the flagged user.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the flagged user (resolved from Discord).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord channel snowflake ID where the event occurred (null for non-message events like raids).
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the channel name where this event occurred (resolved from Discord).
    /// </summary>
    public string ChannelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the rule type that triggered this event.
    /// </summary>
    public RuleType RuleType { get; set; }

    /// <summary>
    /// Gets or sets the severity level of this event.
    /// </summary>
    public Severity Severity { get; set; }

    /// <summary>
    /// Gets or sets the description of what triggered this event.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the evidence (JSON format containing message IDs, content, etc.).
    /// </summary>
    public string Evidence { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of this event.
    /// </summary>
    public FlaggedEventStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the optional action taken by moderators.
    /// </summary>
    public string? ActionTaken { get; set; }

    /// <summary>
    /// Gets or sets the optional Discord user snowflake ID of who reviewed this event.
    /// </summary>
    public ulong? ReviewedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the optional username of who reviewed this event (resolved from Discord).
    /// </summary>
    public string? ReviewedByUsername { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this event was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional timestamp when this event was reviewed (UTC).
    /// </summary>
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>
/// Data transfer object for taking action on a flagged event.
/// </summary>
public class FlaggedEventActionDto
{
    /// <summary>
    /// Gets or sets the ID of the flagged event to act upon.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Gets or sets the new status/action to apply to the event.
    /// </summary>
    public FlaggedEventStatus Action { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator taking this action.
    /// </summary>
    public ulong ReviewerId { get; set; }
}

/// <summary>
/// Data transfer object for detection result from auto-moderation analysis.
/// Used to communicate detection findings from detection services to action handlers.
/// </summary>
public class DetectionResultDto
{
    /// <summary>
    /// Gets or sets the rule type that triggered.
    /// </summary>
    public RuleType RuleType { get; set; }

    /// <summary>
    /// Gets or sets the severity level.
    /// </summary>
    public Severity Severity { get; set; }

    /// <summary>
    /// Gets or sets the description of what was detected.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the evidence data containing rule-specific details about the violation.
    /// Keys and values depend on the RuleType (e.g., matched patterns, message counts, etc.).
    /// </summary>
    public Dictionary<string, object> Evidence { get; set; } = new();

    /// <summary>
    /// Gets or sets whether an auto-action should be taken.
    /// </summary>
    public bool ShouldAutoAction { get; set; }

    /// <summary>
    /// Gets or sets the recommended automatic action to take.
    /// </summary>
    public AutoAction RecommendedAction { get; set; }
}

/// <summary>
/// Data transfer object for guild moderation configuration.
/// </summary>
public class GuildModerationConfigDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the configuration mode.
    /// </summary>
    public ConfigMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the simple mode preset (if using Simple mode).
    /// </summary>
    public string? SimplePreset { get; set; }

    /// <summary>
    /// Gets or sets the spam detection configuration.
    /// </summary>
    public SpamDetectionConfigDto SpamConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the content filter configuration.
    /// </summary>
    public ContentFilterConfigDto ContentFilterConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the raid protection configuration.
    /// </summary>
    public RaidProtectionConfigDto RaidProtectionConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the configuration was last updated (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Data transfer object for spam detection configuration settings.
/// </summary>
public class SpamDetectionConfigDto
{
    /// <summary>
    /// Gets or sets whether spam detection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of messages allowed in the time window.
    /// Default: 5 messages.
    /// </summary>
    public int MaxMessagesPerWindow { get; set; } = 5;

    /// <summary>
    /// Gets or sets the time window in seconds for message counting.
    /// Default: 5 seconds.
    /// </summary>
    public int WindowSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of mentions allowed per message.
    /// Default: 5 mentions.
    /// </summary>
    public int MaxMentionsPerMessage { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum allowed duplicate message similarity (0.0-1.0).
    /// Default: 0.8 (80% similar).
    /// </summary>
    public double DuplicateMessageThreshold { get; set; } = 0.8;

    /// <summary>
    /// Gets or sets the automatic action to take when spam is detected.
    /// </summary>
    public AutoAction AutoAction { get; set; } = AutoAction.Delete;
}

/// <summary>
/// Data transfer object for content filtering configuration settings.
/// </summary>
public class ContentFilterConfigDto
{
    /// <summary>
    /// Gets or sets whether content filtering is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of prohibited words or phrases.
    /// </summary>
    public List<string> ProhibitedWords { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of allowed link domains (whitelist).
    /// Empty list means all links are allowed.
    /// </summary>
    public List<string> AllowedLinkDomains { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to block all links not in the whitelist.
    /// </summary>
    public bool BlockUnlistedLinks { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to block invite links to other Discord servers.
    /// </summary>
    public bool BlockInviteLinks { get; set; } = false;

    /// <summary>
    /// Gets or sets the automatic action to take when prohibited content is detected.
    /// </summary>
    public AutoAction AutoAction { get; set; } = AutoAction.Delete;
}

/// <summary>
/// Data transfer object for raid protection configuration settings.
/// </summary>
public class RaidProtectionConfigDto
{
    /// <summary>
    /// Gets or sets whether raid protection is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of joins allowed in the time window before triggering raid detection.
    /// Default: 10 joins.
    /// </summary>
    public int MaxJoinsPerWindow { get; set; } = 10;

    /// <summary>
    /// Gets or sets the time window in seconds for join counting.
    /// Default: 10 seconds.
    /// </summary>
    public int WindowSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets the minimum account age in hours required to join (0 = no restriction).
    /// Default: 0 (no restriction).
    /// </summary>
    public int MinAccountAgeHours { get; set; } = 0;

    /// <summary>
    /// Gets or sets the automatic action to take when a raid is detected.
    /// </summary>
    public RaidAutoAction AutoAction { get; set; } = RaidAutoAction.AlertOnly;
}

/// <summary>
/// Data transfer object representing a comprehensive user moderation profile.
/// Combines all moderation-related data for a user in a guild: cases, notes, tags, flagged events, and watchlist status.
/// </summary>
public class UserModerationProfileDto
{
    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the user.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the user (resolved from Discord).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID this profile applies to.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the user's Discord account was created (UTC).
    /// </summary>
    public DateTime AccountCreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the user joined this guild (UTC).
    /// </summary>
    public DateTime? JoinedGuildAt { get; set; }

    /// <summary>
    /// Gets or sets the list of moderation cases involving this user.
    /// </summary>
    public IReadOnlyList<ModerationCaseDto> Cases { get; set; } = Array.Empty<ModerationCaseDto>();

    /// <summary>
    /// Gets or sets the list of moderator notes about this user.
    /// </summary>
    public IReadOnlyList<ModNoteDto> Notes { get; set; } = Array.Empty<ModNoteDto>();

    /// <summary>
    /// Gets or sets the list of tags applied to this user.
    /// </summary>
    public IReadOnlyList<UserModTagDto> Tags { get; set; } = Array.Empty<UserModTagDto>();

    /// <summary>
    /// Gets or sets the list of flagged auto-moderation events involving this user.
    /// </summary>
    public IReadOnlyList<FlaggedEventDto> FlaggedEvents { get; set; } = Array.Empty<FlaggedEventDto>();

    /// <summary>
    /// Gets or sets whether this user is currently on the moderator watchlist.
    /// </summary>
    public bool IsOnWatchlist { get; set; }

    /// <summary>
    /// Gets or sets the watchlist entry if the user is on the watchlist.
    /// Null if IsOnWatchlist is false.
    /// </summary>
    public WatchlistEntryDto? WatchlistEntry { get; set; }
}

/// <summary>
/// DTO for reviewing a flagged event.
/// </summary>
public class FlaggedEventReviewDto
{
    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the reviewer.
    /// </summary>
    public ulong ReviewerId { get; set; }
}

/// <summary>
/// DTO for taking action on a flagged event.
/// </summary>
public class FlaggedEventTakeActionDto
{
    /// <summary>
    /// Gets or sets the action description.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the reviewer.
    /// </summary>
    public ulong ReviewerId { get; set; }
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

/// <summary>
/// DTO for applying a tag to a user.
/// </summary>
public class ApplyTagDto
{
    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator applying the tag.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public ulong AppliedById { get; set; }
}

/// <summary>
/// DTO for applying a moderation preset.
/// </summary>
public class ApplyPresetDto
{
    /// <summary>
    /// Gets or sets the preset name (Relaxed, Moderate, or Strict).
    /// </summary>
    public string PresetName { get; set; } = string.Empty;
}

/// <summary>
/// DTO for querying flagged events with filters and pagination.
/// </summary>
public class FlaggedEventQueryDto
{
    /// <summary>
    /// Gets or sets the optional rule type filter.
    /// </summary>
    public RuleType? RuleType { get; set; }

    /// <summary>
    /// Gets or sets the optional severity filter.
    /// </summary>
    public Severity? Severity { get; set; }

    /// <summary>
    /// Gets or sets the optional status filter.
    /// </summary>
    public FlaggedEventStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the optional user ID filter.
    /// </summary>
    public ulong? UserId { get; set; }

    /// <summary>
    /// Gets or sets the optional start date for filtering (UTC).
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// Gets or sets the optional end date for filtering (UTC).
    /// </summary>
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 20;
}
