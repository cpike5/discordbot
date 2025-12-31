namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the configuration mode for guild auto-moderation settings.
/// </summary>
public enum ConfigMode
{
    /// <summary>
    /// Simple mode using preset configurations (Relaxed, Moderate, Strict).
    /// </summary>
    Simple = 0,

    /// <summary>
    /// Advanced mode with full control over all detection parameters.
    /// </summary>
    Advanced = 1
}
