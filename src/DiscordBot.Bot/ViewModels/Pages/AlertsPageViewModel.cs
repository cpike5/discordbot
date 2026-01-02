using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Alerts page, displaying active alerts, incident history, and alert configuration.
/// </summary>
public record AlertsPageViewModel
{
    /// <summary>
    /// Gets the list of currently active incidents requiring attention.
    /// </summary>
    public IReadOnlyList<PerformanceIncidentDto> ActiveIncidents { get; init; } = Array.Empty<PerformanceIncidentDto>();

    /// <summary>
    /// Gets the alert configurations with current metric values.
    /// </summary>
    public IReadOnlyList<AlertConfigDto> AlertConfigs { get; init; } = Array.Empty<AlertConfigDto>();

    /// <summary>
    /// Gets the recent incidents for timeline display (last 10).
    /// </summary>
    public IReadOnlyList<PerformanceIncidentDto> RecentIncidents { get; init; } = Array.Empty<PerformanceIncidentDto>();

    /// <summary>
    /// Gets the auto-recovery events for display.
    /// </summary>
    public IReadOnlyList<AutoRecoveryEventDto> AutoRecoveryEvents { get; init; } = Array.Empty<AutoRecoveryEventDto>();

    /// <summary>
    /// Gets the alert frequency data for chart (last 30 days).
    /// </summary>
    public IReadOnlyList<AlertFrequencyDataDto> AlertFrequencyData { get; init; } = Array.Empty<AlertFrequencyDataDto>();

    /// <summary>
    /// Gets the summary of active alerts by severity.
    /// </summary>
    public ActiveAlertSummaryDto AlertSummary { get; init; } = new();

    /// <summary>
    /// Gets the CSS class for an alert severity level.
    /// </summary>
    /// <param name="severity">The alert severity level.</param>
    /// <returns>CSS class name for styling the severity badge.</returns>
    public static string GetSeverityClass(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "severity-critical",
        AlertSeverity.Warning => "severity-warning",
        AlertSeverity.Info => "severity-info",
        _ => "severity-info"
    };

    /// <summary>
    /// Gets the CSS class for an incident status.
    /// </summary>
    /// <param name="status">The incident status.</param>
    /// <returns>CSS class name for styling the status badge.</returns>
    public static string GetStatusClass(IncidentStatus status) => status switch
    {
        IncidentStatus.Active => "status-badge-error",
        IncidentStatus.Resolved => "status-badge-success",
        IncidentStatus.Acknowledged => "status-badge-warning",
        _ => "status-badge-secondary"
    };

    /// <summary>
    /// Formats a duration in seconds to a human-readable string.
    /// </summary>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <returns>Human-readable duration string (e.g., "5 minutes", "2 hours").</returns>
    public static string FormatDuration(double? durationSeconds)
    {
        if (!durationSeconds.HasValue)
            return "Active";

        var duration = TimeSpan.FromSeconds(durationSeconds.Value);

        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays} day{((int)duration.TotalDays != 1 ? "s" : "")}";

        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours} hour{((int)duration.TotalHours != 1 ? "s" : "")}";

        if (duration.TotalMinutes >= 1)
            return $"{(int)duration.TotalMinutes} minute{((int)duration.TotalMinutes != 1 ? "s" : "")}";

        return $"{(int)duration.TotalSeconds} second{((int)duration.TotalSeconds != 1 ? "s" : "")}";
    }

    /// <summary>
    /// Formats a UTC timestamp to a relative time string (e.g., "5 minutes ago").
    /// </summary>
    /// <param name="utcTime">The UTC timestamp.</param>
    /// <returns>Relative time string.</returns>
    public static string FormatRelativeTime(DateTime utcTime)
    {
        var elapsed = DateTime.UtcNow - utcTime;

        if (elapsed.TotalDays >= 30)
        {
            var months = (int)(elapsed.TotalDays / 30);
            return $"{months} month{(months != 1 ? "s" : "")} ago";
        }

        if (elapsed.TotalDays >= 1)
        {
            var days = (int)elapsed.TotalDays;
            return $"{days} day{(days != 1 ? "s" : "")} ago";
        }

        if (elapsed.TotalHours >= 1)
        {
            var hours = (int)elapsed.TotalHours;
            return $"{hours} hour{(hours != 1 ? "s" : "")} ago";
        }

        if (elapsed.TotalMinutes >= 1)
        {
            var minutes = (int)elapsed.TotalMinutes;
            return $"{minutes} minute{(minutes != 1 ? "s" : "")} ago";
        }

        return "Just now";
    }

    /// <summary>
    /// Gets the timeline dot CSS class based on severity.
    /// </summary>
    /// <param name="severity">The alert severity level.</param>
    /// <returns>CSS class name for the timeline dot.</returns>
    public static string GetTimelineDotClass(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "timeline-dot-error",
        AlertSeverity.Warning => "timeline-dot-warning",
        AlertSeverity.Info => "timeline-dot-info",
        _ => "timeline-dot-info"
    };
}
