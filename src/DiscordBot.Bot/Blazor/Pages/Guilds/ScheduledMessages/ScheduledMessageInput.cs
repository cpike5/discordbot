using System.ComponentModel.DataAnnotations;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Form input model for the scheduled message Create/Edit Blazor pages, with the
/// same validation attributes as the old page models' nested InputModel
/// (Pages/Guilds/ScheduledMessages/Create.cshtml.cs / Edit.cshtml.cs).
/// <see cref="NextExecutionAt"/> holds the user's local wall-clock time (the
/// datetime-local input value); conversion to UTC happens at submit via
/// TimezoneHelper.ConvertToUtc with the browser-detected IANA timezone, exactly
/// as the page models did with the posted UserTimezone hidden field.
/// </summary>
public class ScheduledMessageInput
{
    public ulong GuildId { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Message content is required.")]
    [StringLength(2000, ErrorMessage = "Message content cannot exceed 2000 characters (Discord limit).")]
    [Display(Name = "Message Content")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "Channel is required.")]
    [Display(Name = "Target Channel")]
    public ulong? ChannelId { get; set; }

    [Required(ErrorMessage = "Frequency is required.")]
    [Display(Name = "Schedule Frequency")]
    public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Daily;

    [StringLength(100, ErrorMessage = "Cron expression cannot exceed 100 characters.")]
    [Display(Name = "Cron Expression")]
    public string? CronExpression { get; set; }

    [Display(Name = "Active")]
    public bool IsEnabled { get; set; } = true;

    [Required(ErrorMessage = "Next execution time is required.")]
    [Display(Name = "Next Execution Time")]
    public DateTime? NextExecutionAt { get; set; }
}
