namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result DTO returned after updating settings.
/// </summary>
public record SettingsUpdateResultDto
{
    /// <summary>
    /// Indicates whether the update operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// List of validation or processing errors (empty if successful).
    /// </summary>
    public List<string> Errors { get; init; } = new();

    /// <summary>
    /// Indicates whether a bot restart is required for changes to take effect.
    /// </summary>
    public bool RestartRequired { get; init; }

    /// <summary>
    /// List of setting keys that were successfully updated.
    /// </summary>
    public List<string> UpdatedKeys { get; init; } = new();
}
