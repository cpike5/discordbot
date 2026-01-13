namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object representing a command module configuration for display in the admin UI.
/// </summary>
public record CommandModuleConfigurationDto
{
    /// <summary>
    /// The unique module name identifier.
    /// </summary>
    public string ModuleName { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the module is currently enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// User-friendly display name for the module.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Description of what the module does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Category for grouping modules (Admin, Moderation, Features, Audio, Utility, Core).
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether changes to this module's enabled state require a bot restart.
    /// </summary>
    public bool RequiresRestart { get; init; }

    /// <summary>
    /// Timestamp when the configuration was last modified.
    /// </summary>
    public DateTime LastModifiedAt { get; init; }

    /// <summary>
    /// User ID who last modified the configuration.
    /// </summary>
    public string? LastModifiedBy { get; init; }

    /// <summary>
    /// Number of commands in this module (populated from command metadata).
    /// </summary>
    public int CommandCount { get; init; }

    /// <summary>
    /// List of command names in this module (populated from command metadata).
    /// </summary>
    public IReadOnlyList<string> Commands { get; init; } = Array.Empty<string>();
}
