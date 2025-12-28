namespace DiscordBot.Core.Utilities;

/// <summary>
/// Provides timezone conversion utilities using IANA timezone names.
/// </summary>
public static class TimezoneHelper
{
    /// <summary>
    /// Converts a local DateTime to UTC using the specified IANA timezone.
    /// </summary>
    /// <param name="localDateTime">The local datetime in the specified timezone (DateTimeKind will be ignored).</param>
    /// <param name="ianaTimezoneName">The IANA timezone identifier (e.g., "America/New_York").</param>
    /// <returns>The UTC datetime.</returns>
    /// <remarks>
    /// If the timezone is invalid or null, returns the input datetime assuming it's already UTC.
    /// </remarks>
    public static DateTime ConvertToUtc(DateTime localDateTime, string? ianaTimezoneName)
    {
        if (string.IsNullOrWhiteSpace(ianaTimezoneName))
        {
            // Assume already UTC if no timezone provided
            return DateTime.SpecifyKind(localDateTime, DateTimeKind.Utc);
        }

        var timeZone = GetTimeZoneInfo(ianaTimezoneName);

        if (timeZone.Id == "UTC")
        {
            // Fallback occurred, treat input as UTC
            return DateTime.SpecifyKind(localDateTime, DateTimeKind.Utc);
        }

        // Convert from the specified timezone to UTC
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, timeZone);
    }

    /// <summary>
    /// Converts a UTC DateTime to local time in the specified IANA timezone.
    /// </summary>
    /// <param name="utcDateTime">The UTC datetime.</param>
    /// <param name="ianaTimezoneName">The IANA timezone identifier (e.g., "America/New_York").</param>
    /// <returns>The local datetime in the specified timezone.</returns>
    /// <remarks>
    /// If the timezone is invalid or null, returns the UTC datetime unchanged.
    /// </remarks>
    public static DateTime ConvertFromUtc(DateTime utcDateTime, string? ianaTimezoneName)
    {
        if (string.IsNullOrWhiteSpace(ianaTimezoneName))
        {
            return utcDateTime;
        }

        var timeZone = GetTimeZoneInfo(ianaTimezoneName);

        // Convert from UTC to the specified timezone
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
    }

    /// <summary>
    /// Validates whether the provided string is a valid IANA timezone identifier.
    /// </summary>
    /// <param name="ianaTimezoneName">The timezone identifier to validate.</param>
    /// <returns>True if the timezone is valid; otherwise, false.</returns>
    public static bool IsValidTimezone(string? ianaTimezoneName)
    {
        if (string.IsNullOrWhiteSpace(ianaTimezoneName))
        {
            return false;
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(ianaTimezoneName);
            return tz != null;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets TimeZoneInfo from IANA timezone name, with fallback to UTC.
    /// </summary>
    /// <param name="ianaTimezoneName">The IANA timezone identifier.</param>
    /// <returns>The TimeZoneInfo object, or UTC if the timezone is invalid.</returns>
    public static TimeZoneInfo GetTimeZoneInfo(string? ianaTimezoneName)
    {
        if (string.IsNullOrWhiteSpace(ianaTimezoneName))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaTimezoneName);
        }
        catch (TimeZoneNotFoundException)
        {
            // Invalid timezone, fallback to UTC
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            // Invalid timezone format, fallback to UTC
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Gets the timezone abbreviation for display (e.g., "EST", "PST").
    /// </summary>
    /// <param name="ianaTimezoneName">The IANA timezone identifier.</param>
    /// <param name="forDateTime">The datetime to get the abbreviation for (considers DST).</param>
    /// <returns>The timezone abbreviation, or empty string if unavailable.</returns>
    public static string GetTimezoneAbbreviation(string? ianaTimezoneName, DateTime forDateTime)
    {
        if (string.IsNullOrWhiteSpace(ianaTimezoneName))
        {
            return "UTC";
        }

        var timeZone = GetTimeZoneInfo(ianaTimezoneName);

        if (timeZone.Id == "UTC")
        {
            return "UTC";
        }

        // Get the display name (abbreviation)
        // This will return something like "EST" or "EDT" depending on DST
        return timeZone.IsDaylightSavingTime(forDateTime)
            ? timeZone.DaylightName
            : timeZone.StandardName;
    }
}
