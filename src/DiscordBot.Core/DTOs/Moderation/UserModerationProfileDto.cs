using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

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
