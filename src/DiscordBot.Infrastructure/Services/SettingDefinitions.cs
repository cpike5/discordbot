using DiscordBot.Core.Enums;
using System.Text.Json;

namespace DiscordBot.Infrastructure.Services;

/// <summary>
/// Metadata definition for a single setting.
/// </summary>
public record SettingDefinition
{
    public string Key { get; init; }
    public string DisplayName { get; init; }
    public SettingCategory Category { get; init; }
    public SettingDataType DataType { get; init; }
    public string DefaultValue { get; init; }
    public bool RequiresRestart { get; init; }
    public string? Description { get; init; }
    public List<string>? AllowedValues { get; init; }
    public string? ValidationRules { get; init; }

    public SettingDefinition(
        string key,
        string displayName,
        SettingCategory category,
        SettingDataType dataType,
        string defaultValue,
        bool requiresRestart,
        string? description = null,
        List<string>? allowedValues = null,
        object? validation = null)
    {
        Key = key;
        DisplayName = displayName;
        Category = category;
        DataType = dataType;
        DefaultValue = defaultValue;
        RequiresRestart = requiresRestart;
        Description = description;
        AllowedValues = allowedValues;
        ValidationRules = validation != null ? JsonSerializer.Serialize(validation) : null;
    }
}

/// <summary>
/// Static registry of all available application settings with their metadata.
/// </summary>
public static class SettingDefinitions
{
    private static readonly List<string> Timezones = new()
    {
        "UTC",
        "America/New_York",
        "America/Chicago",
        "America/Denver",
        "America/Los_Angeles",
        "Europe/London",
        "Europe/Paris",
        "Europe/Berlin",
        "Asia/Tokyo",
        "Asia/Shanghai",
        "Australia/Sydney"
    };

    /// <summary>
    /// All available settings with their metadata.
    /// </summary>
    public static readonly IReadOnlyList<SettingDefinition> All = new List<SettingDefinition>
    {
        // General Category
        new(
            key: "General:DefaultTimezone",
            displayName: "Default Timezone",
            category: SettingCategory.General,
            dataType: SettingDataType.String,
            defaultValue: "UTC",
            requiresRestart: false,
            description: "Timezone used for scheduled tasks and timestamp displays",
            allowedValues: Timezones
        ),
        new(
            key: "General:StatusMessage",
            displayName: "Bot Status Message",
            category: SettingCategory.General,
            dataType: SettingDataType.String,
            defaultValue: "",
            requiresRestart: false,
            description: "Custom status message shown in Discord (leave empty for default)"
        ),
        new(
            key: "General:BotEnabled",
            displayName: "Bot Enabled",
            category: SettingCategory.General,
            dataType: SettingDataType.Boolean,
            defaultValue: "true",
            requiresRestart: false,
            description: "Enable or disable the bot without stopping the service"
        ),

        // Features Category
        new(
            key: "Features:MessageLoggingEnabled",
            displayName: "Message Logging",
            category: SettingCategory.Features,
            dataType: SettingDataType.Boolean,
            defaultValue: "true",
            requiresRestart: false,
            description: "Enable or disable Discord message logging globally"
        ),
        new(
            key: "Features:WelcomeMessagesEnabled",
            displayName: "Welcome Messages",
            category: SettingCategory.Features,
            dataType: SettingDataType.Boolean,
            defaultValue: "true",
            requiresRestart: false,
            description: "Enable or disable welcome messages globally (in addition to per-guild settings)"
        ),
        new(
            key: "Features:RatWatchEnabled",
            displayName: "Rat Watch",
            category: SettingCategory.Features,
            dataType: SettingDataType.Boolean,
            defaultValue: "true",
            requiresRestart: false,
            description: "Enable or disable the Rat Watch accountability feature globally"
        ),

        // Advanced Category
        new(
            key: "Advanced:MessageLogRetentionDays",
            displayName: "Message Log Retention (Days)",
            category: SettingCategory.Advanced,
            dataType: SettingDataType.Integer,
            defaultValue: "90",
            requiresRestart: false,
            description: "Number of days to retain Discord message logs before deletion",
            validation: new { min = 1, max = 365 }
        ),
        new(
            key: "Advanced:AuditLogRetentionDays",
            displayName: "Audit Log Retention (Days)",
            category: SettingCategory.Advanced,
            dataType: SettingDataType.Integer,
            defaultValue: "90",
            requiresRestart: false,
            description: "Number of days to retain audit log entries before deletion",
            validation: new { min = 1, max = 365 }
        )
    };

    /// <summary>
    /// Gets a setting definition by key.
    /// </summary>
    public static SettingDefinition? GetByKey(string key)
    {
        return All.FirstOrDefault(s => s.Key == key);
    }

    /// <summary>
    /// Gets all setting definitions for a category.
    /// </summary>
    public static IReadOnlyList<SettingDefinition> GetByCategory(SettingCategory category)
    {
        return All.Where(s => s.Category == category).ToList();
    }
}
