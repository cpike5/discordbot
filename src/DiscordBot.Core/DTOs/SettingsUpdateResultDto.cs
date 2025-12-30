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

    /// <summary>
    /// Dictionary of settings that were actually changed, with old and new values.
    /// Key is the setting key, value is a tuple of (OldValue, NewValue).
    /// </summary>
    public Dictionary<string, SettingChange> Changes { get; init; } = new();
}

/// <summary>
/// Represents a change to a setting value.
/// </summary>
public record SettingChange
{
    /// <summary>
    /// The value before the change.
    /// </summary>
    public string? OldValue { get; init; }

    /// <summary>
    /// The value after the change.
    /// </summary>
    public string? NewValue { get; init; }

    /// <summary>
    /// The display name of the setting.
    /// </summary>
    public string? DisplayName { get; init; }
}
