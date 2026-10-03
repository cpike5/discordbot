using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// How a <see cref="RatWatchStatus"/> reads on screen and in exports. One place, so the filter
/// checkboxes, the table, the incident dialog and the CSV all say the same thing and never show
/// an enum name like <c>ClearedEarly</c> (UX plan C-2).
/// </summary>
public static class RatWatchStatusDisplay
{
    /// <summary>The status in plain words: "Cleared early", "Not guilty".</summary>
    public static string DisplayName(this RatWatchStatus status) => status switch
    {
        RatWatchStatus.Pending => "Pending",
        RatWatchStatus.ClearedEarly => "Cleared early",
        RatWatchStatus.Voting => "Voting",
        RatWatchStatus.Guilty => "Guilty",
        RatWatchStatus.NotGuilty => "Not guilty",
        RatWatchStatus.Expired => "Expired",
        RatWatchStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };
}
