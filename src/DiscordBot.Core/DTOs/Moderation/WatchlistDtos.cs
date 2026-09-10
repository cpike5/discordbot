using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

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
