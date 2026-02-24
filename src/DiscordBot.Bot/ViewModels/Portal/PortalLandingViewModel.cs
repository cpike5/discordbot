namespace DiscordBot.Bot.ViewModels.Portal;

/// <summary>
/// ViewModel for the shared portal landing page partial.
/// </summary>
public class PortalLandingViewModel
{
    /// <summary>
    /// Gets or sets the guild display name.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL (null for fallback).
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Gets or sets the Discord OAuth login URL.
    /// </summary>
    public string LoginUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the portal badge text (e.g., "Soundboard Portal", "TTS Portal", "VOX Portal").
    /// </summary>
    public string PortalBadgeText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description text shown below the badge.
    /// </summary>
    public string DescriptionText { get; set; } = string.Empty;
}
