namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents the configuration state for a Discord bot command module.
/// Enables administrators to enable/disable command modules through the admin UI.
/// </summary>
public class CommandModuleConfiguration
{
    /// <summary>
    /// The unique module name identifier (e.g., "AdminModule", "RatWatchModule").
    /// This is the primary key.
    /// </summary>
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether the module is currently enabled.
    /// Disabled modules will not respond to commands.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// User-friendly display name for the module in the admin UI.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what the module does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category for grouping modules in the admin UI.
    /// Valid values: Admin, Moderation, Features, Audio, Utility, Core.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether changes to this module's enabled state require a bot restart.
    /// </summary>
    public bool RequiresRestart { get; set; } = true;

    /// <summary>
    /// Timestamp when the configuration was last modified.
    /// </summary>
    public DateTime LastModifiedAt { get; set; }

    /// <summary>
    /// User ID who last modified the configuration (nullable for system-created defaults).
    /// </summary>
    public string? LastModifiedBy { get; set; }
}
