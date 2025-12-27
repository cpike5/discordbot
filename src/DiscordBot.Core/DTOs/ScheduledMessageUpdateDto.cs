using System.ComponentModel.DataAnnotations;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for updating an existing scheduled message.
/// All properties are nullable to support partial updates (PATCH).
/// </summary>
public class ScheduledMessageUpdateDto
{
    /// <summary>
    /// Gets or sets the Discord channel snowflake ID where this message will be sent. Null means no change.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the title of the scheduled message. Null means no change.
    /// </summary>
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the content of the scheduled message. Null means no change.
    /// </summary>
    [MaxLength(2000, ErrorMessage = "Content cannot exceed 2000 characters (Discord message limit).")]
    public string? Content { get; set; }

    /// <summary>
    /// Gets or sets the optional cron expression for custom schedules. Null means no change.
    /// Required when Frequency is set to Custom.
    /// </summary>
    [MaxLength(100, ErrorMessage = "Cron expression cannot exceed 100 characters.")]
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the frequency at which this message should be sent. Null means no change.
    /// </summary>
    public ScheduleFrequency? Frequency { get; set; }

    /// <summary>
    /// Gets or sets whether this scheduled message is currently active and should be processed. Null means no change.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this message should next be sent. Null means no change.
    /// </summary>
    public DateTime? NextExecutionAt { get; set; }
}
