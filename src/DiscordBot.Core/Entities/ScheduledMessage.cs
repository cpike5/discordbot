using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a scheduled message that will be sent to a Discord channel at specified intervals.
/// </summary>
public class ScheduledMessage
{
    /// <summary>
    /// Unique identifier for this scheduled message.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this message will be sent.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord channel snowflake ID where this message will be sent.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Title of the scheduled message.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Content of the scheduled message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional cron expression for custom schedules.
    /// Only used when Frequency is set to Custom.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Frequency at which this message should be sent.
    /// </summary>
    public ScheduleFrequency Frequency { get; set; }

    /// <summary>
    /// Whether this scheduled message is currently active and should be processed.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Timestamp when this message was last sent.
    /// Null if the message has never been sent.
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Timestamp when this message should next be sent.
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }

    /// <summary>
    /// Timestamp when this scheduled message was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Identifier of the user who created this scheduled message.
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this scheduled message was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this scheduled message belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
