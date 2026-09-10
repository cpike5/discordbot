using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

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
