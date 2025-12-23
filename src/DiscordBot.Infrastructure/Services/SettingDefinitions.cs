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
    private static readonly List<string> LogLevels = new()
    {
        "Trace",
        "Debug",
        "Information",
        "Warning",
        "Error",
        "Critical"
    };

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

        // Logging Category
        new(
            key: "Serilog:MinimumLevel:Default",
            displayName: "Minimum Log Level",
            category: SettingCategory.Logging,
            dataType: SettingDataType.String,
            defaultValue: "Information",
            requiresRestart: true,
            description: "Minimum severity level for log messages to be recorded",
            allowedValues: LogLevels
        ),
        new(
            key: "Logging:RetainedFileCountLimit",
            displayName: "Log Retention (Days)",
            category: SettingCategory.Logging,
            dataType: SettingDataType.Integer,
            defaultValue: "7",
            requiresRestart: true,
            description: "Number of days to retain log files before deletion",
            validation: new { min = 1, max = 90 }
        ),

        // Features Category
        new(
            key: "Discord:DefaultRateLimitInvokes",
            displayName: "Rate Limit Invocations",
            category: SettingCategory.Features,
            dataType: SettingDataType.Integer,
            defaultValue: "3",
            requiresRestart: false,
            description: "Maximum number of command invocations allowed before rate limiting",
            validation: new { min = 1, max = 100 }
        ),
        new(
            key: "Discord:DefaultRateLimitPeriodSeconds",
            displayName: "Rate Limit Period (Seconds)",
            category: SettingCategory.Features,
            dataType: SettingDataType.Decimal,
            defaultValue: "60",
            requiresRestart: false,
            description: "Time window in seconds for rate limit enforcement",
            validation: new { min = 10, max = 3600 }
        ),

        // Advanced Category
        new(
            key: "Advanced:DebugMode",
            displayName: "Debug Mode",
            category: SettingCategory.Advanced,
            dataType: SettingDataType.Boolean,
            defaultValue: "false",
            requiresRestart: true,
            description: "Enable verbose debug logging and additional diagnostics"
        ),
        new(
            key: "Advanced:CacheEnabled",
            displayName: "Enable Caching",
            category: SettingCategory.Advanced,
            dataType: SettingDataType.Boolean,
            defaultValue: "true",
            requiresRestart: false,
            description: "Enable in-memory caching for improved performance"
        ),
        new(
            key: "Advanced:DataRetentionDays",
            displayName: "Data Retention (Days)",
            category: SettingCategory.Advanced,
            dataType: SettingDataType.Integer,
            defaultValue: "90",
            requiresRestart: false,
            description: "Number of days to retain historical data (logs, analytics)",
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
