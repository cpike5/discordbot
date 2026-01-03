namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for displaying user preview popup data.
/// </summary>
public record UserPreviewViewModel
{
    /// <summary>
    /// Discord user snowflake ID.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Discord username (e.g., "username" or "username#0000" for legacy).
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
    public IReadOnlyList<string> Roles { get; init; } = [];

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

    /// <summary>
    /// Guild context for the preview (optional).
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// URL to full profile page.
    /// </summary>
    public string ProfileUrl { get; init; } = string.Empty;

    /// <summary>
    /// URL to moderation history page (null if not available).
    /// </summary>
    public string? ModerationHistoryUrl { get; init; }
}
