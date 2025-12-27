namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the frequency at which a scheduled message should be sent.
/// </summary>
public enum ScheduleFrequency
{
    /// <summary>
    /// Message should be sent only once at the specified time.
    /// </summary>
    Once = 1,

    /// <summary>
    /// Message should be sent every hour.
    /// </summary>
    Hourly = 2,

    /// <summary>
    /// Message should be sent once per day.
    /// </summary>
    Daily = 3,

    /// <summary>
    /// Message should be sent once per week.
    /// </summary>
    Weekly = 4,

    /// <summary>
    /// Message should be sent once per month.
    /// </summary>
    Monthly = 5,

    /// <summary>
    /// Message should be sent according to a custom cron expression.
    /// </summary>
    Custom = 6
}
