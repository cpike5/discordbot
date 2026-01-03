namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for displaying guild preview popup data.
/// </summary>
public record GuildPreviewViewModel
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
    public IReadOnlyList<string> ActiveFeatures { get; init; } = [];

    /// <summary>
    /// Whether the guild is currently active (bot connected).
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// URL to guild details page.
    /// </summary>
    public string DetailsUrl { get; init; } = string.Empty;

    /// <summary>
    /// URL to guild settings page.
    /// </summary>
    public string SettingsUrl { get; init; } = string.Empty;
}
