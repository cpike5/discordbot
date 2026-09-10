using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

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
