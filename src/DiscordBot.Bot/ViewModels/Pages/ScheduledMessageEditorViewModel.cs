using DiscordBot.Bot.Helpers;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Utilities;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// What the shared scheduled-message form (<c>_MessageEditor.cshtml</c>) renders, for both the
/// create and the edit page. Built from the stored message on GET and from the submitted input
/// after a failed POST.
/// </summary>
public class ScheduledMessageEditorViewModel
{
    public ulong GuildId { get; set; }

    public string GuildName { get; set; } = string.Empty;

    /// <summary>True on the edit page: shows the Delete action, status and run history.</summary>
    public bool IsEdit { get; set; }

    public Guid? MessageId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public ulong? ChannelId { get; set; }

    public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Daily;

    public string? CronExpression { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// The value of the date-time field exactly as the user typed it, in their own time zone
    /// (<c>yyyy-MM-ddTHH:mm</c>). Set after a failed POST. It is already local, so no script may
    /// convert it again: converting it as if it were UTC is what shifted the time by the UTC offset
    /// after every failed save.
    /// </summary>
    public string? NextExecutionLocal { get; set; }

    /// <summary>
    /// The stored next run as a UTC instant, for the edit page's first render. A script converts it
    /// into the viewer's time zone and fills the field. Null once the field holds what the user typed.
    /// </summary>
    public string? NextExecutionUtcIso { get; set; }

    /// <summary>The next run as a UTC instant, for the read-only "Next run" summary.</summary>
    public string? SummaryNextRunUtcIso { get; set; }

    public DateTime? LastExecutedAt { get; set; }

    public List<ChannelSelectItem> AvailableChannels { get; set; } = new();

    /// <summary>True after a failed POST: the form shows input that is not saved yet.</summary>
    public bool DirtyOnLoad { get; set; }

    /// <summary>Paused, Active or Expired, for the edit page.</summary>
    public string StatusText => !IsEnabled
        ? "Paused"
        : Frequency == ScheduleFrequency.Once && LastExecutedAt.HasValue ? "Expired" : "Active";

    /// <summary>The form-field format for a date-time-local input.</summary>
    public const string LocalFormat = "yyyy-MM-ddTHH:mm";

    /// <summary>
    /// The UTC instant a submitted local time stands for, as an ISO string, or null when there is
    /// nothing to convert. Used to keep the "Next run" summary right after a failed POST.
    /// </summary>
    public static string? ToUtcIso(DateTime? local, string? timeZone)
    {
        // A time the zone skips (daylight-saving gap) stands for no instant: show no summary
        if (!local.HasValue || TimezoneHelper.IsNonexistentLocalTime(local.Value, timeZone))
        {
            return null;
        }

        return DisplayFormat.Iso(TimezoneHelper.ConvertToUtc(local.Value, timeZone));
    }
}
