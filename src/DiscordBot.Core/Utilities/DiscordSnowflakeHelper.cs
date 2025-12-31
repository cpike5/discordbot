namespace DiscordBot.Core.Utilities;

/// <summary>
/// Utility methods for working with Discord snowflake IDs.
/// </summary>
public static class DiscordSnowflakeHelper
{
    /// <summary>
    /// Discord epoch (January 1, 2015) in Unix milliseconds.
    /// </summary>
    private const long DiscordEpoch = 1420070400000;

    /// <summary>
    /// Extracts the creation timestamp from a Discord snowflake ID.
    /// Discord snowflake format: timestamp (42 bits) | worker id (5 bits) | process id (5 bits) | increment (12 bits)
    /// </summary>
    /// <param name="snowflakeId">The Discord snowflake ID.</param>
    /// <returns>The DateTime when the snowflake was created (UTC).</returns>
    public static DateTime GetCreationTimestamp(ulong snowflakeId)
    {
        var unixTimestampMs = (long)(snowflakeId >> 22) + DiscordEpoch;
        return DateTimeOffset.FromUnixTimeMilliseconds(unixTimestampMs).UtcDateTime;
    }

    /// <summary>
    /// Extracts the creation timestamp from a Discord snowflake ID.
    /// </summary>
    /// <param name="snowflakeId">The Discord snowflake ID.</param>
    /// <returns>The DateTimeOffset when the snowflake was created.</returns>
    public static DateTimeOffset GetCreationDateTimeOffset(ulong snowflakeId)
    {
        var unixTimestampMs = (long)(snowflakeId >> 22) + DiscordEpoch;
        return DateTimeOffset.FromUnixTimeMilliseconds(unixTimestampMs);
    }
}
