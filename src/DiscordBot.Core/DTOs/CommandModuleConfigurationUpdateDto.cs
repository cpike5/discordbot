namespace DiscordBot.Core.DTOs;

/// <summary>
/// Request DTO for updating command module configurations.
/// </summary>
public record CommandModuleConfigurationUpdateDto
{
    /// <summary>
    /// Dictionary of module names to their new enabled state.
    /// </summary>
    public Dictionary<string, bool> Modules { get; init; } = new();
}

/// <summary>
/// Result DTO for command module configuration update operations.
/// </summary>
public record CommandModuleUpdateResultDto
{
    /// <summary>
    /// Indicates whether the update was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Indicates whether a bot restart is required for changes to take effect.
    /// </summary>
    public bool RequiresRestart { get; init; }

    /// <summary>
    /// List of module names that were successfully updated.
    /// </summary>
    public IReadOnlyList<string> UpdatedModules { get; init; } = Array.Empty<string>();

    /// <summary>
    /// List of validation errors that occurred during the update.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}
