using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for creating a new Rat Watch.
/// Used when a watch is initiated via context menu modal.
/// </summary>
public record RatWatchCreateDto
{
    /// <summary>
    /// Discord guild snowflake ID where this watch is created.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// Discord channel snowflake ID where this watch was created.
    /// </summary>
    public ulong ChannelId { get; init; }

    /// <summary>
    /// Discord user snowflake ID of the user being watched (the accused).
    /// </summary>
    public ulong AccusedUserId { get; init; }

    /// <summary>
    /// Discord user snowflake ID of the user who initiated the watch.
    /// </summary>
    public ulong InitiatorUserId { get; init; }

    /// <summary>
    /// Discord message snowflake ID of the original message that triggered the watch.
    /// </summary>
    public ulong OriginalMessageId { get; init; }

    /// <summary>
    /// Optional custom message describing the commitment or reason for the watch.
    /// </summary>
    public string? CustomMessage { get; init; }

    /// <summary>
    /// Timestamp when the rat check should occur (UTC).
    /// </summary>
    public DateTime ScheduledAt { get; init; }
}

/// <summary>
/// Data transfer object for displaying Rat Watch information.
/// Includes username fields resolved from Discord.
/// </summary>
public record RatWatchDto
{
    /// <summary>
    /// Unique identifier for this Rat Watch.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Discord guild snowflake ID where this watch was created.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// Discord channel snowflake ID where this watch was created.
    /// </summary>
    public ulong ChannelId { get; init; }

    /// <summary>
    /// Discord user snowflake ID of the user being watched (the accused).
    /// </summary>
    public ulong AccusedUserId { get; init; }

    /// <summary>
    /// Username of the accused user (resolved from Discord).
    /// </summary>
    public string AccusedUsername { get; init; } = string.Empty;

    /// <summary>
    /// Discord user snowflake ID of the user who initiated the watch.
    /// </summary>
    public ulong InitiatorUserId { get; init; }

    /// <summary>
    /// Username of the initiator user (resolved from Discord).
    /// </summary>
    public string InitiatorUsername { get; init; } = string.Empty;

    /// <summary>
    /// Discord message snowflake ID of the original message that triggered the watch.
    /// </summary>
    public ulong OriginalMessageId { get; init; }

    /// <summary>
    /// Optional custom message describing the commitment or reason for the watch.
    /// </summary>
    public string? CustomMessage { get; init; }

    /// <summary>
    /// Timestamp when the rat check should occur (UTC).
    /// </summary>
    public DateTime ScheduledAt { get; init; }

    /// <summary>
    /// Timestamp when this watch was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Current status of the Rat Watch.
    /// </summary>
    public RatWatchStatus Status { get; init; }

    /// <summary>
    /// Number of guilty votes cast.
    /// </summary>
    public int GuiltyVotes { get; init; }

    /// <summary>
    /// Number of not guilty votes cast.
    /// </summary>
    public int NotGuiltyVotes { get; init; }
}

/// <summary>
/// Data transfer object for user Rat Watch statistics.
/// </summary>
public record RatStatsDto
{
    /// <summary>
    /// Discord user snowflake ID.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Username of the user (resolved from Discord).
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Total number of guilty verdicts for this user.
    /// </summary>
    public int TotalGuiltyCount { get; init; }

    /// <summary>
    /// Recent guilty records for this user.
    /// </summary>
    public IReadOnlyList<RatRecordDto> RecentRecords { get; init; } = Array.Empty<RatRecordDto>();
}

/// <summary>
/// Data transfer object for a single Rat Record.
/// </summary>
public record RatRecordDto
{
    /// <summary>
    /// Timestamp when the record was created (UTC).
    /// </summary>
    public DateTime RecordedAt { get; init; }

    /// <summary>
    /// Number of guilty votes in the verdict.
    /// </summary>
    public int GuiltyVotes { get; init; }

    /// <summary>
    /// Number of not guilty votes in the verdict.
    /// </summary>
    public int NotGuiltyVotes { get; init; }

    /// <summary>
    /// Link to the original message that triggered the watch.
    /// Format: https://discord.com/channels/{guildId}/{channelId}/{messageId}
    /// </summary>
    public string? OriginalMessageLink { get; init; }
}

/// <summary>
/// Data transfer object for a leaderboard entry.
/// </summary>
public record RatLeaderboardEntryDto
{
    /// <summary>
    /// Rank position on the leaderboard (1-based).
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Discord user snowflake ID.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Username of the user (resolved from Discord).
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Total number of guilty verdicts for this user.
    /// </summary>
    public int GuiltyCount { get; init; }
}
