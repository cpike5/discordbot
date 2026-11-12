namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// The one mapping from an overall health status ("Healthy", "Warning", "Critical", "Offline") to
/// the words and badge class the Performance pages show. A status nobody recognises reads
/// "Unknown" in a neutral badge, never "Operational".
/// </summary>
public static class HealthStatusDisplay
{
    /// <summary>
    /// The status the aggregator reports while the gateway is not connected.
    /// </summary>
    public const string Offline = "Offline";

    /// <summary>
    /// Gets the display text and CSS class for an overall health status.
    /// </summary>
    public static (string Text, string CssClass) For(string? status) =>
        (status ?? string.Empty).ToLowerInvariant() switch
        {
            "healthy" => ("Operational", "health-status-healthy"),
            "warning" => ("Degraded", "health-status-warning"),
            "critical" => ("Critical", "health-status-error"),
            "offline" => ("Offline", "health-status-error"),
            _ => ("Unknown", "health-status-neutral")
        };
}
