using System.ComponentModel.DataAnnotations;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for creating a new scheduled message.
/// </summary>
public class ScheduledMessageCreateDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this message will be sent.
    /// </summary>
    [Required(ErrorMessage = "Guild ID is required.")]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord channel snowflake ID where this message will be sent.
    /// </summary>
    [Required(ErrorMessage = "Channel ID is required.")]
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the title of the scheduled message.
    /// </summary>
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the scheduled message.
    /// </summary>
    [Required(ErrorMessage = "Content is required.")]
    [MaxLength(2000, ErrorMessage = "Content cannot exceed 2000 characters (Discord message limit).")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional cron expression for custom schedules.
    /// Required when Frequency is set to Custom.
    /// </summary>
    [MaxLength(100, ErrorMessage = "Cron expression cannot exceed 100 characters.")]
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the frequency at which this message should be sent.
    /// </summary>
    [Required(ErrorMessage = "Frequency is required.")]
    public ScheduleFrequency Frequency { get; set; }

    /// <summary>
    /// Gets or sets whether this scheduled message is currently active and should be processed.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the timestamp when this message should first be sent.
    /// </summary>
    [Required(ErrorMessage = "Next execution time is required.")]
    public DateTime NextExecutionAt { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user creating this scheduled message.
    /// </summary>
    [Required(ErrorMessage = "Created by is required.")]
    [MaxLength(450, ErrorMessage = "Created by cannot exceed 450 characters.")]
    public string CreatedBy { get; set; } = string.Empty;
}
