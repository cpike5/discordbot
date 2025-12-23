using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents an application setting that can be configured through the admin UI.
/// Settings are stored as key-value pairs with metadata for validation and display.
/// </summary>
public class ApplicationSetting
{
    /// <summary>
    /// Unique setting key (e.g., "General:DefaultTimezone").
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Setting value (stored as string, converted based on DataType).
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Category this setting belongs to for UI organization.
    /// </summary>
    public SettingCategory Category { get; set; }

    /// <summary>
    /// Data type of the setting value for validation and conversion.
    /// </summary>
    public SettingDataType DataType { get; set; }

    /// <summary>
    /// Indicates whether changing this setting requires a bot restart to take effect.
    /// </summary>
    public bool RequiresRestart { get; set; }

    /// <summary>
    /// Timestamp when the setting was last modified.
    /// </summary>
    public DateTime LastModifiedAt { get; set; }

    /// <summary>
    /// User ID who last modified the setting (nullable for system-created defaults).
    /// </summary>
    public string? LastModifiedBy { get; set; }
}
