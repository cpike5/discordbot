namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for user preview popup data.
/// </summary>
public record UserPreviewDto
{
    /// <summary>
    /// Discord user snowflake ID.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Discord username.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Display name (nickname or global display name).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Avatar URL (Discord CDN).
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// When user joined the guild (null if no guild context).
    /// </summary>
    public DateTime? MemberSince { get; init; }

    /// <summary>
    /// Top roles (limit to 3-5 for display).
    /// </summary>
    public List<string> Roles { get; init; } = [];

    /// <summary>
    /// Last activity timestamp from command logs or events.
    /// </summary>
    public DateTime? LastActive { get; init; }

    /// <summary>
    /// Whether user is verified in the system.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// Whether user has an active moderation case.
    /// </summary>
    public bool HasActiveModeration { get; init; }
}

/// <summary>
/// Data transfer object for guild preview popup data.
/// </summary>
public record GuildPreviewDto
{
    /// <summary>
    /// Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// Guild name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Guild icon URL (Discord CDN).
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Total member count.
    /// </summary>
    public int MemberCount { get; init; }

    /// <summary>
    /// Online member count (if available).
    /// </summary>
    public int? OnlineMemberCount { get; init; }

    /// <summary>
    /// Guild owner username.
    /// </summary>
    public string OwnerUsername { get; init; } = string.Empty;

    /// <summary>
    /// When the bot joined this guild.
    /// </summary>
    public DateTime BotJoinedAt { get; init; }

    /// <summary>
    /// Active features (e.g., "Moderation", "RatWatch", "Welcome").
    /// </summary>
    public List<string> ActiveFeatures { get; init; } = [];

    /// <summary>
    /// Whether the guild is currently active (bot connected).
    /// </summary>
    public bool IsActive { get; init; }
}
