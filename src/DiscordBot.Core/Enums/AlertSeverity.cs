namespace DiscordBot.Core.Enums;

/// <summary>
/// Severity levels for performance alerts.
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Informational alert - metric is within normal range but noteworthy.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning alert - metric exceeded warning threshold.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Critical alert - metric exceeded critical threshold.
    /// </summary>
    Critical = 2
}
