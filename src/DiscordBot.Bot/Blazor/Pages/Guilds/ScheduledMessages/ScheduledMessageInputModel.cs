using System.ComponentModel.DataAnnotations;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.ScheduledMessages;

/// <summary>
/// Shared <c>EditForm</c> model for Create and Edit (docs/plans/blazor-port-plan.md §5 Phase 4,
/// cluster 4b "share one model class") - mirrors the legacy <c>CreateModel.InputModel</c>/
/// <c>EditModel.InputModel</c> field-for-field, including their data annotations.
/// <see cref="NextExecutionAt"/> holds the value as typed into the
/// <c>&lt;InputDate Type="InputDateType.DateTimeLocal"&gt;</c> field - i.e. in the viewer's detected
/// local timezone, not UTC; the page converts it with <c>TimezoneHelper.ConvertToUtc</c> on submit
/// and with <c>TimezoneHelper.ConvertFromUtc</c> when prefilling an existing message.
/// </summary>
public sealed class ScheduledMessageInputModel
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Message content is required.")]
    [StringLength(2000, ErrorMessage = "Message content cannot exceed 2000 characters (Discord limit).")]
    public string Content { get; set; } = string.Empty;

    [Required(ErrorMessage = "Channel is required.")]
    public ulong? ChannelId { get; set; }

    [Required(ErrorMessage = "Frequency is required.")]
    public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Daily;

    [StringLength(100, ErrorMessage = "Cron expression cannot exceed 100 characters.")]
    public string? CronExpression { get; set; }

    public bool IsEnabled { get; set; } = true;

    [Required(ErrorMessage = "Next execution time is required.")]
    public DateTime? NextExecutionAt { get; set; }
}
