using DiscordBot.Core.DTOs;
using System.Text.Json;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying detailed guild information.
/// </summary>
public record GuildDetailViewModel
{
    /// <summary>
    /// Gets the guild's Discord snowflake ID.
    /// </summary>
    public ulong Id { get; init; }

    /// <summary>
    /// Gets the guild name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the member count.
    /// </summary>
    public int MemberCount { get; init; }

    /// <summary>
    /// Gets the guild icon URL.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Gets whether the guild is active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets the date when the bot joined the guild.
    /// </summary>
    public DateTime JoinedAt { get; init; }

    /// <summary>
    /// Gets the custom command prefix for the guild.
    /// </summary>
    public string? Prefix { get; init; }

    /// <summary>
    /// Gets the parsed guild settings.
    /// </summary>
    public GuildSettingsViewModel Settings { get; init; } = new();

    /// <summary>
    /// Gets the list of recent command logs for this guild.
    /// </summary>
    public IReadOnlyList<RecentCommandLogItem> RecentCommandLogs { get; init; } = Array.Empty<RecentCommandLogItem>();

    /// <summary>
    /// Gets whether the current user can edit this guild's settings.
    /// </summary>
    public bool CanEdit { get; init; }

    /// <summary>
    /// Creates a <see cref="GuildDetailViewModel"/> from a <see cref="GuildDto"/>.
    /// </summary>
    /// <param name="dto">The guild DTO to map from.</param>
    /// <param name="recentLogs">Optional collection of recent command logs for the guild.</param>
    /// <returns>A new <see cref="GuildDetailViewModel"/> instance.</returns>
    public static GuildDetailViewModel FromDto(GuildDto dto, IEnumerable<CommandLogDto>? recentLogs = null)
    {
        return new GuildDetailViewModel
        {
            Id = dto.Id,
            Name = dto.Name,
            MemberCount = dto.MemberCount ?? 0,
            IconUrl = dto.IconUrl,
            IsActive = dto.IsActive,
            JoinedAt = dto.JoinedAt,
            Prefix = dto.Prefix,
            Settings = GuildSettingsViewModel.Parse(dto.Settings),
            RecentCommandLogs = recentLogs?.Select(RecentCommandLogItem.FromDto).ToList() ?? (IReadOnlyList<RecentCommandLogItem>)Array.Empty<RecentCommandLogItem>(),
            CanEdit = true // Will be set by PageModel based on authorization
        };
    }
}

/// <summary>
/// Represents a recent command log entry for guild detail display.
/// </summary>
public record RecentCommandLogItem
{
    /// <summary>
    /// Gets the unique identifier for the command log entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the username of the user who executed the command.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the name of the command that was executed.
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the timestamp when the command was executed.
    /// </summary>
    public DateTime ExecutedAt { get; init; }

    /// <summary>
    /// Gets the response time in milliseconds.
    /// </summary>
    public int ResponseTimeMs { get; init; }

    /// <summary>
    /// Gets whether the command executed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the error message if the command failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a <see cref="RecentCommandLogItem"/> from a <see cref="CommandLogDto"/>.
    /// </summary>
    /// <param name="dto">The command log DTO to map from.</param>
    /// <returns>A new <see cref="RecentCommandLogItem"/> instance.</returns>
    public static RecentCommandLogItem FromDto(CommandLogDto dto)
    {
        return new RecentCommandLogItem
        {
            Id = dto.Id,
            Username = dto.Username ?? "Unknown",
            CommandName = dto.CommandName,
            ExecutedAt = dto.ExecutedAt,
            ResponseTimeMs = dto.ResponseTimeMs,
            Success = dto.Success,
            ErrorMessage = dto.ErrorMessage
        };
    }
}

/// <summary>
/// Parsed guild settings for display.
/// </summary>
public record GuildSettingsViewModel
{
    /// <summary>
    /// Gets the welcome channel ID or name.
    /// </summary>
    public string? WelcomeChannel { get; init; }

    /// <summary>
    /// Gets the log channel ID or name.
    /// </summary>
    public string? LogChannel { get; init; }

    /// <summary>
    /// Gets whether auto-moderation is enabled.
    /// </summary>
    public bool AutoModEnabled { get; init; }

    /// <summary>
    /// Gets whether welcome messages are enabled.
    /// </summary>
    public bool WelcomeMessagesEnabled { get; init; }

    /// <summary>
    /// Gets whether leave messages are enabled.
    /// </summary>
    public bool LeaveMessagesEnabled { get; init; }

    /// <summary>
    /// Gets whether moderation alerts are enabled.
    /// </summary>
    public bool ModerationAlertsEnabled { get; init; }

    /// <summary>
    /// Gets whether command logging is enabled.
    /// </summary>
    public bool CommandLoggingEnabled { get; init; }

    /// <summary>
    /// Gets whether any custom settings are configured.
    /// </summary>
    public bool HasSettings => !string.IsNullOrEmpty(WelcomeChannel)
        || !string.IsNullOrEmpty(LogChannel)
        || AutoModEnabled
        || WelcomeMessagesEnabled
        || LeaveMessagesEnabled
        || ModerationAlertsEnabled
        || CommandLoggingEnabled;

    /// <summary>
    /// Parses guild settings from JSON.
    /// </summary>
    /// <param name="settingsJson">The JSON settings blob from the database.</param>
    /// <returns>A parsed GuildSettingsViewModel instance.</returns>
    public static GuildSettingsViewModel Parse(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson))
            return new GuildSettingsViewModel();

        try
        {
            return JsonSerializer.Deserialize<GuildSettingsViewModel>(settingsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new GuildSettingsViewModel();
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return empty settings
            return new GuildSettingsViewModel();
        }
    }
}
