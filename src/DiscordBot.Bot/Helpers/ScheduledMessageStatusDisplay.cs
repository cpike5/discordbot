using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// The Active/Paused/Expired status computation duplicated three times for scheduled messages -
/// <c>ViewModels/Pages/ScheduledMessageListViewModel.cs</c>'s <c>ScheduledMessageListItem</c> (list
/// row) and the deleted <c>Pages/Guilds/ScheduledMessages/Edit.cshtml.cs</c>'s
/// <c>GetStatusDisplay</c>/<c>GetStatusBadgeClass</c>/<c>GetStatusDotClass</c> (edit-page header) -
/// one shared helper for <c>Blazor/Pages/Guilds/ScheduledMessages/*.razor</c>.
/// <see cref="ScheduledMessageListViewModel.ScheduledMessageListItem"/>'s own properties now
/// delegate here too, so the list and the edit page can never drift.
/// </summary>
/// <remarks>
/// Lives in <c>Helpers/</c>, not <c>Blazor/Common/</c>, even though every current caller is a
/// Blazor page: <c>ViewModels/Pages/ScheduledMessageListViewModel.cs</c> is the legacy Razor Pages
/// view-model tree, which must stay framework-neutral (no reference to the Blazor tree it is being
/// ported away from) - see docs review finding on cluster 4b.
/// </remarks>
public static class ScheduledMessageStatusDisplay
{
    /// <summary>"Active", "Paused", or "Expired" - the exact wording the legacy pages used.</summary>
    public static string Label(bool isEnabled, ScheduleFrequency frequency, DateTime? lastExecutedAt, DateTime? nextExecutionAt)
    {
        if (!isEnabled)
        {
            return "Paused";
        }

        if (frequency == ScheduleFrequency.Once && lastExecutedAt.HasValue)
        {
            return "Expired";
        }

        if (frequency == ScheduleFrequency.Once && nextExecutionAt.HasValue && nextExecutionAt.Value < DateTime.UtcNow)
        {
            return "Expired";
        }

        return "Active";
    }

    public static BadgeVariant Variant(string label) => label switch
    {
        "Active" => BadgeVariant.Success,
        "Paused" => BadgeVariant.Warning,
        "Expired" => BadgeVariant.Default,
        _ => BadgeVariant.Default
    };

    /// <summary>Tailwind background-color utility for the small status dot inside the badge.</summary>
    public static string DotClass(string label) => label switch
    {
        "Active" => "bg-success",
        "Paused" => "bg-warning",
        "Expired" => "bg-text-tertiary",
        _ => "bg-text-tertiary"
    };

    public static string ScheduleDescription(ScheduleFrequency frequency) => frequency switch
    {
        ScheduleFrequency.Once => "One-time",
        ScheduleFrequency.Hourly => "Every hour",
        ScheduleFrequency.Daily => "Daily",
        ScheduleFrequency.Weekly => "Weekly",
        ScheduleFrequency.Monthly => "Monthly",
        ScheduleFrequency.Custom => "Custom schedule",
        _ => "Unknown"
    };
}
