using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the scheduled message form (shared by Create and Edit pages).
/// </summary>
public class ScheduledMessageFormViewModel
{
    /// <summary>
    /// Gets or sets the guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name (display only).
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL (display only).
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Gets or sets the title of the scheduled message.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target channel ID.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the schedule frequency.
    /// </summary>
    public ScheduleFrequency Frequency { get; set; }

    /// <summary>
    /// Gets or sets the cron expression (for custom schedules).
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets whether this scheduled message is enabled.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the next execution time.
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }

    /// <summary>
    /// Gets or sets the list of available text channels in the guild.
    /// </summary>
    public List<ChannelSelectItem> AvailableChannels { get; set; } = new();

    /// <summary>
    /// Gets or sets whether this is edit mode (true) or create mode (false).
    /// </summary>
    public bool IsEditMode { get; set; }
}
