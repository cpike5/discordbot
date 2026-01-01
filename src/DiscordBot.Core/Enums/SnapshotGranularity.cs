namespace DiscordBot.Core.Enums;

/// <summary>
/// Defines the time granularity for analytics snapshots.
/// </summary>
public enum SnapshotGranularity
{
    /// <summary>
    /// Hourly aggregation (one snapshot per hour).
    /// </summary>
    Hourly = 0,

    /// <summary>
    /// Daily aggregation (one snapshot per day, 00:00-23:59 UTC).
    /// </summary>
    Daily = 1
}
