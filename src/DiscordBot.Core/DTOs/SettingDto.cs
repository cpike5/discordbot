using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object representing a setting with its metadata for display in the UI.
/// </summary>
public record SettingDto
{
    /// <summary>
    /// Unique setting key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Current setting value.
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Category this setting belongs to.
    /// </summary>
    public SettingCategory Category { get; init; }

    /// <summary>
    /// Data type of the setting value.
    /// </summary>
    public SettingDataType DataType { get; init; }

    /// <summary>
    /// Indicates whether changing this setting requires a bot restart.
    /// </summary>
    public bool RequiresRestart { get; init; }

    /// <summary>
    /// Display name for the setting (user-friendly).
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Description explaining what the setting does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// JSON-encoded validation rules (e.g., {"min": 1, "max": 100}).
    /// </summary>
    public string? ValidationRules { get; init; }

    /// <summary>
    /// List of allowed values for dropdown controls (null if free-form input).
    /// </summary>
    public List<string>? AllowedValues { get; init; }

    /// <summary>
    /// Default value from configuration.
    /// </summary>
    public string? DefaultValue { get; init; }
}
