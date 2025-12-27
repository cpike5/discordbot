using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for scheduled message information in listings and details views.
/// </summary>
public class ScheduledMessageDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this scheduled message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this message will be sent.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the name of the guild for display purposes.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    /// Gets or sets the Discord channel snowflake ID where this message will be sent.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the title of the scheduled message.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the scheduled message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional cron expression for custom schedules.
    /// Only used when Frequency is set to Custom.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the frequency at which this message should be sent.
    /// </summary>
    public ScheduleFrequency Frequency { get; set; }

    /// <summary>
    /// Gets or sets whether this scheduled message is currently active and should be processed.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this message was last sent.
    /// Null if the message has never been sent.
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this message should next be sent.
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this scheduled message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who created this scheduled message.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this scheduled message was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets the time remaining until the next execution.
    /// Null if NextExecutionAt is not set or if the scheduled message is not enabled.
    /// </summary>
    public TimeSpan? TimeUntilNext
    {
        get
        {
            if (!IsEnabled || !NextExecutionAt.HasValue)
            {
                return null;
            }

            var timeUntil = NextExecutionAt.Value - DateTime.UtcNow;
            return timeUntil.TotalSeconds > 0 ? timeUntil : TimeSpan.Zero;
        }
    }
}
