using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the user moderation profile page.
/// Displays all moderation-related data for a specific user in a guild.
/// </summary>
public class UserModerationProfileViewModel
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord user snowflake ID.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the user's display name (nickname or username).
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user's avatar URL.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the user's Discord account was created (UTC).
    /// </summary>
    public DateTime AccountCreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the user joined this guild (UTC).
    /// </summary>
    public DateTime? JoinedGuildAt { get; set; }

    /// <summary>
    /// Gets or sets the list of roles assigned to the user in this guild.
    /// </summary>
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the list of moderation cases for this user.
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
    /// Gets or sets the list of flagged events involving this user.
    /// </summary>
    public IReadOnlyList<FlaggedEventDto> FlaggedEvents { get; set; } = Array.Empty<FlaggedEventDto>();

    /// <summary>
    /// Gets or sets the list of available tags that can be applied to users in this guild.
    /// </summary>
    public IReadOnlyList<ModTagDto> AvailableTags { get; set; } = Array.Empty<ModTagDto>();

    /// <summary>
    /// Gets or sets the current authenticated user's Discord ID (for identifying note authors, etc.).
    /// </summary>
    public ulong CurrentUserId { get; set; }
}
