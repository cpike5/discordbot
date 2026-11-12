using DiscordBot.Bot.ViewModels.Components;
using Microsoft.AspNetCore.Http;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Chips for the date-range filter that analytics-style pages share (<c>_DateRangeFilter</c>:
/// <c>StartDate</c> and <c>EndDate</c>), so each page does not rebuild the same label and URL.
/// </summary>
public static class FilterChipHelper
{
    /// <summary>
    /// The date range as one chip, or none when the URL does not set one (the page's default range
    /// is not a filter the person chose). Removing it drops both dates and goes back to page 1.
    /// </summary>
    public static IReadOnlyList<ActiveFilterChip> DateRange(HttpRequest request, DateTime? start, DateTime? end)
    {
        var chosen = request.Query.ContainsKey("StartDate") || request.Query.ContainsKey("EndDate");
        if (!chosen || (!start.HasValue && !end.HasValue))
        {
            return Array.Empty<ActiveFilterChip>();
        }

        var from = start.HasValue ? start.Value.ToString("MMM d, yyyy") : "any time";
        var to = end.HasValue ? end.Value.ToString("MMM d, yyyy") : "now";
        return new[] { new ActiveFilterChip($"Dates: {from} – {to}", QueryStringHelper.Without(request, "StartDate", "EndDate")) };
    }
}
