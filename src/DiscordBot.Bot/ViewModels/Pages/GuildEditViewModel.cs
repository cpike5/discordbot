using DiscordBot.Core.DTOs;
using System.Text.Json;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the guild settings edit form.
/// </summary>
public class GuildEditViewModel
{
    /// <summary>
    /// Gets or sets the guild's Discord snowflake ID.
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Gets or sets the guild name (display only).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL (display only).
    /// </summary>
    public string? IconUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the guild is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the custom command prefix (1-3 characters).
    /// </summary>
    public string? Prefix { get; set; }

    // Notification Settings

    /// <summary>
    /// Gets or sets whether welcome messages are enabled.
    /// </summary>
    public bool WelcomeMessagesEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether leave messages are enabled.
    /// </summary>
    public bool LeaveMessagesEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether moderation alerts are enabled.
    /// </summary>
    public bool ModerationAlertsEnabled { get; set; }

    /// <summary>
    /// Gets or sets whether command logging is enabled.
    /// </summary>
    public bool CommandLoggingEnabled { get; set; }

    // Advanced Settings

    /// <summary>
    /// Gets or sets the welcome channel name or ID.
    /// </summary>
    public string? WelcomeChannel { get; set; }

    /// <summary>
    /// Gets or sets the log channel name or ID.
    /// </summary>
    public string? LogChannel { get; set; }

    /// <summary>
    /// Gets or sets whether auto-moderation is enabled.
    /// </summary>
    public bool AutoModEnabled { get; set; }

    /// <summary>
    /// Creates a GuildEditViewModel from a GuildDto.
    /// </summary>
    public static GuildEditViewModel FromDto(GuildDto dto)
    {
        var settings = ParseSettings(dto.Settings);

        return new GuildEditViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            IconUrl = dto.IconUrl,
            IsActive = dto.IsActive,
            Prefix = dto.Prefix,
            WelcomeMessagesEnabled = settings.WelcomeMessagesEnabled,
            LeaveMessagesEnabled = settings.LeaveMessagesEnabled,
            ModerationAlertsEnabled = settings.ModerationAlertsEnabled,
            CommandLoggingEnabled = settings.CommandLoggingEnabled,
            WelcomeChannel = settings.WelcomeChannel,
            LogChannel = settings.LogChannel,
            AutoModEnabled = settings.AutoModEnabled
        };
    }

    /// <summary>
    /// Serializes the settings properties to JSON for storage.
    /// </summary>
    public string ToSettingsJson()
    {
        var settings = new GuildSettingsData
        {
            WelcomeChannel = WelcomeChannel,
            LogChannel = LogChannel,
            AutoModEnabled = AutoModEnabled,
            WelcomeMessagesEnabled = WelcomeMessagesEnabled,
            LeaveMessagesEnabled = LeaveMessagesEnabled,
            ModerationAlertsEnabled = ModerationAlertsEnabled,
            CommandLoggingEnabled = CommandLoggingEnabled
        };

        return JsonSerializer.Serialize(settings, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static GuildSettingsData ParseSettings(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson))
            return new GuildSettingsData();

        try
        {
            return JsonSerializer.Deserialize<GuildSettingsData>(settingsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new GuildSettingsData();
        }
        catch (JsonException)
        {
            return new GuildSettingsData();
        }
    }

    /// <summary>
    /// Internal data structure for JSON serialization.
    /// </summary>
    private class GuildSettingsData
    {
        public string? WelcomeChannel { get; set; }
        public string? LogChannel { get; set; }
        public bool AutoModEnabled { get; set; }
        public bool WelcomeMessagesEnabled { get; set; }
        public bool LeaveMessagesEnabled { get; set; }
        public bool ModerationAlertsEnabled { get; set; }
        public bool CommandLoggingEnabled { get; set; }
    }
}
