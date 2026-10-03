namespace DiscordBot.Bot.Services.Performance;

/// <summary>
/// The tabs of the Performance dashboard shell (<c>/Admin/Performance?tab=…</c>) and the
/// query-string values that select them. One place for the ids, the URLs that point at a tab
/// (notification links, the redirects from the retired standalone pages) and the time-range
/// clamp, so no caller re-implements them.
/// </summary>
public static class PerformanceDashboardTabs
{
    /// <summary>The shell's route.</summary>
    public const string ShellPath = "/Admin/Performance";

    /// <summary>The Overview tab id (the default).</summary>
    public const string Overview = "overview";

    /// <summary>The Health Metrics tab id.</summary>
    public const string Health = "health";

    /// <summary>The Commands tab id.</summary>
    public const string Commands = "commands";

    /// <summary>The API and rate limits tab id.</summary>
    public const string Api = "api";

    /// <summary>The System Health tab id.</summary>
    public const string System = "system";

    /// <summary>The Alerts tab id.</summary>
    public const string Alerts = "alerts";

    /// <summary>Every tab id, in display order.</summary>
    public static readonly IReadOnlyList<string> TabIds = new[] { Overview, Health, Commands, Api, System, Alerts };

    /// <summary>The time ranges the shell offers: 24 hours, 7 days, 30 days.</summary>
    public static readonly IReadOnlyList<int> AllowedHours = new[] { 24, 168, 720 };

    /// <summary>The default time range in hours.</summary>
    public const int DefaultHours = 24;

    /// <summary>
    /// A known tab id in lower case, or <see cref="Overview"/> for anything else (including null).
    /// </summary>
    public static string NormalizeTab(string? tab)
    {
        var id = tab?.Trim().ToLowerInvariant();
        return id is not null && TabIds.Contains(id) ? id : Overview;
    }

    /// <summary>True when <paramref name="tab"/> is one of <see cref="TabIds"/> (case-insensitive).</summary>
    public static bool IsKnownTab(string? tab)
    {
        var id = tab?.Trim().ToLowerInvariant();
        return id is not null && TabIds.Contains(id);
    }

    /// <summary>
    /// Clamps a requested time range to the supported ones: anything up to 24 (including zero and
    /// negative values) is 24, up to 168 is 168, and anything longer is 720.
    /// </summary>
    public static int NormalizeHours(int hours) => hours switch
    {
        <= 24 => 24,
        <= 168 => 168,
        _ => 720
    };

    /// <summary>
    /// The URL of a tab in the shell. The time range is included only when it is not the default.
    /// </summary>
    public static string TabUrl(string tab, int? hours = null)
    {
        var url = $"{ShellPath}?tab={NormalizeTab(tab)}";
        if (hours is { } h && NormalizeHours(h) != DefaultHours)
        {
            url += $"&hours={NormalizeHours(h)}";
        }

        return url;
    }
}
