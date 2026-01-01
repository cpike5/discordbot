using DiscordBot.Core.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for parsing time expressions into UTC DateTime values.
/// Supports relative times, absolute times, day names, dates, and special keywords.
/// </summary>
public class TimeParsingService : ITimeParsingService
{
    private readonly ILogger<TimeParsingService> _logger;

    // Month name mappings (case-insensitive)
    private static readonly Dictionary<string, int> MonthNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "jan", 1 }, { "january", 1 },
        { "feb", 2 }, { "february", 2 },
        { "mar", 3 }, { "march", 3 },
        { "apr", 4 }, { "april", 4 },
        { "may", 5 },
        { "jun", 6 }, { "june", 6 },
        { "jul", 7 }, { "july", 7 },
        { "aug", 8 }, { "august", 8 },
        { "sep", 9 }, { "sept", 9 }, { "september", 9 },
        { "oct", 10 }, { "october", 10 },
        { "nov", 11 }, { "november", 11 },
        { "dec", 12 }, { "december", 12 }
    };

    // Day name mappings (case-insensitive)
    private static readonly Dictionary<string, DayOfWeek> DayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        { "monday", DayOfWeek.Monday }, { "mon", DayOfWeek.Monday },
        { "tuesday", DayOfWeek.Tuesday }, { "tue", DayOfWeek.Tuesday }, { "tues", DayOfWeek.Tuesday },
        { "wednesday", DayOfWeek.Wednesday }, { "wed", DayOfWeek.Wednesday },
        { "thursday", DayOfWeek.Thursday }, { "thu", DayOfWeek.Thursday }, { "thur", DayOfWeek.Thursday }, { "thurs", DayOfWeek.Thursday },
        { "friday", DayOfWeek.Friday }, { "fri", DayOfWeek.Friday },
        { "saturday", DayOfWeek.Saturday }, { "sat", DayOfWeek.Saturday },
        { "sunday", DayOfWeek.Sunday }, { "sun", DayOfWeek.Sunday }
    };

    public TimeParsingService(ILogger<TimeParsingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public TimeParseResult Parse(string input, string timezone)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            _logger.LogDebug("Parse failed: input is null or whitespace");
            return TimeParseResult.Error("Time input cannot be empty");
        }

        input = input.Trim().ToLowerInvariant();
        _logger.LogDebug("Parsing time expression: '{Input}' with timezone '{Timezone}'", input, timezone);

        // Validate and get timezone
        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid timezone '{Timezone}', using UTC", timezone);
            timeZone = TimeZoneInfo.Utc;
        }

        // Try parsing in order of specificity
        var result = TryParseRelativeTime(input)
            ?? TryParseSpecialKeyword(input, timeZone)
            ?? TryParseFullDateTime(input, timeZone)
            ?? TryParseDateWithTime(input, timeZone)
            ?? TryParseDayNameWithTime(input, timeZone)
            ?? TryParseAbsoluteTime(input, timeZone);

        if (result == null)
        {
            _logger.LogDebug("Failed to parse time expression: '{Input}'", input);
            return TimeParseResult.Error("Unable to parse time expression. Use formats like: 10m, 2h, tomorrow 3pm, Dec 31, noon");
        }

        // Validate that the time is in the future
        if (result.UtcTime <= DateTime.UtcNow)
        {
            _logger.LogDebug("Parsed time '{ParsedTime}' is in the past", result.UtcTime);
            return TimeParseResult.Error("Scheduled time must be in the future");
        }

        _logger.LogInformation("Successfully parsed '{Input}' to UTC time {UtcTime} (type: {ParseType})",
            input, result.UtcTime, result.ParseType);

        return result;
    }

    /// <summary>
    /// Tries to parse relative time formats like "10m", "2h", "1d", "1w", "1h30m", "2d 4h".
    /// </summary>
    private TimeParseResult? TryParseRelativeTime(string input)
    {
        // Remove optional "in " prefix
        input = Regex.Replace(input, @"^in\s+", "", RegexOptions.IgnoreCase);

        // Pattern: supports weeks, days, hours, and minutes in various combinations
        // Examples: "1w", "2d", "3h", "30m", "1w 2d", "1d 4h", "2h 30m", "1h30m"
        var pattern = @"^(?:(\d+)\s*w(?:eeks?)?)?(?:\s*(\d+)\s*d(?:ays?)?)?(?:\s*(\d+)\s*h(?:ours?)?)?(?:\s*(\d+)\s*m(?:in(?:utes?)?)?)?$";
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        var weeks = 0;
        var days = 0;
        var hours = 0;
        var minutes = 0;

        if (match.Groups[1].Success && int.TryParse(match.Groups[1].Value, out var w))
        {
            weeks = w;
        }

        if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var d))
        {
            days = d;
        }

        if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var h))
        {
            hours = h;
        }

        if (match.Groups[4].Success && int.TryParse(match.Groups[4].Value, out var m))
        {
            minutes = m;
        }

        // At least one time component must be present
        if (weeks == 0 && days == 0 && hours == 0 && minutes == 0)
        {
            return null;
        }

        var utcTime = DateTime.UtcNow
            .AddDays(weeks * 7)
            .AddDays(days)
            .AddHours(hours)
            .AddMinutes(minutes);

        _logger.LogDebug("Parsed relative time: {Weeks}w {Days}d {Hours}h {Minutes}m -> {UtcTime}",
            weeks, days, hours, minutes, utcTime);

        return TimeParseResult.Ok(utcTime, TimeParseType.Relative);
    }

    /// <summary>
    /// Tries to parse special time keywords: noon, midnight, morning, afternoon, evening, night.
    /// </summary>
    private TimeParseResult? TryParseSpecialKeyword(string input, TimeZoneInfo timeZone)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var today = now.Date;

        DateTime localTime;
        switch (input)
        {
            case "noon":
                localTime = today.AddHours(12);
                break;
            case "midnight":
                localTime = today.AddDays(1); // Midnight of next day
                break;
            case "morning":
                localTime = today.AddHours(9); // 9 AM
                break;
            case "afternoon":
                localTime = today.AddHours(15); // 3 PM
                break;
            case "evening":
                localTime = today.AddHours(18); // 6 PM
                break;
            case "night":
                localTime = today.AddHours(21); // 9 PM
                break;
            default:
                return null;
        }

        // If the time has passed today, schedule for tomorrow
        if (localTime <= now)
        {
            localTime = localTime.AddDays(1);
        }

        var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);

        _logger.LogDebug("Parsed special keyword '{Input}' to local time {LocalTime} (UTC: {UtcTime})",
            input, localTime, utcTime);

        return TimeParseResult.Ok(utcTime, TimeParseType.AbsoluteTime);
    }

    /// <summary>
    /// Tries to parse full datetime formats like "2024-12-31 10:00" or "2025-01-01T14:30".
    /// </summary>
    private TimeParseResult? TryParseFullDateTime(string input, TimeZoneInfo timeZone)
    {
        // Try ISO 8601 format with T separator
        var patterns = new[]
        {
            @"^(\d{4})-(\d{1,2})-(\d{1,2})T(\d{1,2}):(\d{2})(?::(\d{2}))?$",
            @"^(\d{4})-(\d{1,2})-(\d{1,2})\s+(\d{1,2}):(\d{2})(?::(\d{2}))?$",
            @"^(\d{1,2})/(\d{1,2})/(\d{4})\s+(\d{1,2}):(\d{2})(?::(\d{2}))?\s*(am|pm)?$"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            int year, month, day, hour, minute, second = 0;

            // Handle different date orderings
            if (pattern.Contains(@"\d{4}"))
            {
                // YYYY-MM-DD format
                if (!int.TryParse(match.Groups[1].Value, out year) ||
                    !int.TryParse(match.Groups[2].Value, out month) ||
                    !int.TryParse(match.Groups[3].Value, out day) ||
                    !int.TryParse(match.Groups[4].Value, out hour) ||
                    !int.TryParse(match.Groups[5].Value, out minute))
                {
                    continue;
                }

                if (match.Groups[6].Success)
                {
                    int.TryParse(match.Groups[6].Value, out second);
                }
            }
            else
            {
                // MM/DD/YYYY format
                if (!int.TryParse(match.Groups[1].Value, out month) ||
                    !int.TryParse(match.Groups[2].Value, out day) ||
                    !int.TryParse(match.Groups[3].Value, out year) ||
                    !int.TryParse(match.Groups[4].Value, out hour) ||
                    !int.TryParse(match.Groups[5].Value, out minute))
                {
                    continue;
                }

                if (match.Groups[6].Success)
                {
                    int.TryParse(match.Groups[6].Value, out second);
                }

                // Handle AM/PM
                if (match.Groups[7].Success)
                {
                    var ampm = match.Groups[7].Value.ToLowerInvariant();
                    if (ampm == "pm" && hour < 12)
                    {
                        hour += 12;
                    }
                    else if (ampm == "am" && hour == 12)
                    {
                        hour = 0;
                    }
                }
            }

            // Validate date components
            if (year < DateTime.UtcNow.Year || year > DateTime.UtcNow.Year + 10 ||
                month < 1 || month > 12 ||
                day < 1 || day > DateTime.DaysInMonth(year, month) ||
                hour < 0 || hour > 23 ||
                minute < 0 || minute > 59 ||
                second < 0 || second > 59)
            {
                continue;
            }

            try
            {
                var localTime = new DateTime(year, month, day, hour, minute, second);
                var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);

                _logger.LogDebug("Parsed full datetime '{Input}' to local time {LocalTime} (UTC: {UtcTime})",
                    input, localTime, utcTime);

                return TimeParseResult.Ok(utcTime, TimeParseType.FullDateTime);
            }
            catch (ArgumentOutOfRangeException)
            {
                continue;
            }
        }

        return null;
    }

    /// <summary>
    /// Tries to parse date with optional time like "Dec 31", "Jan 1 10pm", "january 15 14:30".
    /// </summary>
    private TimeParseResult? TryParseDateWithTime(string input, TimeZoneInfo timeZone)
    {
        // Pattern: month name/abbreviation, day number, optional time
        var pattern = @"^([a-z]+)\s+(\d{1,2})(?:\s+(.+))?$";
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        var monthStr = match.Groups[1].Value;
        if (!MonthNames.TryGetValue(monthStr, out var month))
        {
            return null;
        }

        if (!int.TryParse(match.Groups[2].Value, out var day) || day < 1 || day > 31)
        {
            return null;
        }

        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var year = now.Year;

        // Validate day is valid for the month
        if (day > DateTime.DaysInMonth(year, month))
        {
            return null;
        }

        // Parse optional time component
        var hour = 0;
        var minute = 0;

        if (match.Groups[3].Success)
        {
            var timeStr = match.Groups[3].Value.Trim();
            var timeResult = ParseTimeComponent(timeStr);
            if (timeResult == null)
            {
                return null;
            }

            hour = timeResult.Value.hour;
            minute = timeResult.Value.minute;
        }

        try
        {
            var localTime = new DateTime(year, month, day, hour, minute, 0);

            // If the date has passed this year, schedule for next year
            if (localTime <= now)
            {
                year++;
                // Validate the date is still valid in the next year (handles leap year edge cases)
                if (day > DateTime.DaysInMonth(year, month))
                {
                    return null;
                }
                localTime = new DateTime(year, month, day, hour, minute, 0);
            }

            var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);

            _logger.LogDebug("Parsed date with time '{Input}' to local time {LocalTime} (UTC: {UtcTime})",
                input, localTime, utcTime);

            return TimeParseResult.Ok(utcTime, TimeParseType.AbsoluteDate);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tries to parse day names with optional time like "tomorrow", "monday", "next friday 3pm".
    /// </summary>
    private TimeParseResult? TryParseDayNameWithTime(string input, TimeZoneInfo timeZone)
    {
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var today = now.Date;

        // Handle "tomorrow" and "today"
        if (input.StartsWith("tomorrow"))
        {
            var timeStr = input.Substring("tomorrow".Length).Trim();
            var localTime = today.AddDays(1);

            if (!string.IsNullOrEmpty(timeStr))
            {
                var timeResult = ParseTimeComponent(timeStr);
                if (timeResult == null)
                {
                    return null;
                }

                localTime = localTime.AddHours(timeResult.Value.hour).AddMinutes(timeResult.Value.minute);
            }

            var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);

            _logger.LogDebug("Parsed 'tomorrow' to local time {LocalTime} (UTC: {UtcTime})",
                localTime, utcTime);

            return TimeParseResult.Ok(utcTime, TimeParseType.AbsoluteDay);
        }

        if (input.StartsWith("today"))
        {
            var timeStr = input.Substring("today".Length).Trim();
            var localTime = today;

            if (!string.IsNullOrEmpty(timeStr))
            {
                var timeResult = ParseTimeComponent(timeStr);
                if (timeResult == null)
                {
                    return null;
                }

                localTime = localTime.AddHours(timeResult.Value.hour).AddMinutes(timeResult.Value.minute);
            }
            else
            {
                // Default to end of day if no time specified
                localTime = localTime.AddHours(23).AddMinutes(59);
            }

            // If the time has passed today, it's invalid
            if (localTime <= now)
            {
                return null;
            }

            var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);

            _logger.LogDebug("Parsed 'today' to local time {LocalTime} (UTC: {UtcTime})",
                localTime, utcTime);

            return TimeParseResult.Ok(utcTime, TimeParseType.AbsoluteDay);
        }

        // Handle day names with optional "next" prefix
        var isNext = input.StartsWith("next ");
        var searchInput = isNext ? input.Substring(5) : input;

        foreach (var (dayName, targetDayOfWeek) in DayNames)
        {
            if (!searchInput.StartsWith(dayName))
            {
                continue;
            }

            var timeStr = searchInput.Substring(dayName.Length).Trim();
            var daysUntilTarget = ((int)targetDayOfWeek - (int)now.DayOfWeek + 7) % 7;

            // If "next" is specified or the day has passed this week, go to next week
            if (isNext || daysUntilTarget == 0)
            {
                daysUntilTarget = daysUntilTarget == 0 ? 7 : daysUntilTarget;
            }

            var localTime = today.AddDays(daysUntilTarget);

            if (!string.IsNullOrEmpty(timeStr))
            {
                var timeResult = ParseTimeComponent(timeStr);
                if (timeResult == null)
                {
                    return null;
                }

                localTime = localTime.AddHours(timeResult.Value.hour).AddMinutes(timeResult.Value.minute);
            }

            var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);

            _logger.LogDebug("Parsed day name '{Input}' to local time {LocalTime} (UTC: {UtcTime})",
                input, localTime, utcTime);

            return TimeParseResult.Ok(utcTime, TimeParseType.AbsoluteDay);
        }

        return null;
    }

    /// <summary>
    /// Tries to parse absolute time formats like "10pm", "22:00", "10:30pm", "14:30".
    /// </summary>
    private TimeParseResult? TryParseAbsoluteTime(string input, TimeZoneInfo timeZone)
    {
        var timeResult = ParseTimeComponent(input);
        if (timeResult == null)
        {
            return null;
        }

        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var today = now.Date;

        var localTime = new DateTime(today.Year, today.Month, today.Day, timeResult.Value.hour, timeResult.Value.minute, 0);

        // If the time has already passed today, schedule for tomorrow
        if (localTime <= now)
        {
            localTime = localTime.AddDays(1);
        }

        var utcTime = TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);

        _logger.LogDebug("Parsed absolute time '{Input}' to local time {LocalTime} (UTC: {UtcTime})",
            input, localTime, utcTime);

        return TimeParseResult.Ok(utcTime, TimeParseType.AbsoluteTime);
    }

    /// <summary>
    /// Parses a time component string into hour and minute.
    /// Supports formats: "10pm", "10:30pm", "22:00", "14:30".
    /// </summary>
    private (int hour, int minute)? ParseTimeComponent(string input)
    {
        input = input.Trim();

        // Try 12-hour format with am/pm (e.g., "10pm", "10:30pm")
        var pattern12Hour = @"^(\d{1,2})(?::(\d{2}))?\s*(am|pm)$";
        var match = Regex.Match(input, pattern12Hour, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            if (!int.TryParse(match.Groups[1].Value, out var hour) || hour < 1 || hour > 12)
            {
                return null;
            }

            var minute = 0;
            if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var m))
            {
                if (m < 0 || m > 59)
                {
                    return null;
                }
                minute = m;
            }

            var isPm = match.Groups[3].Value.ToLowerInvariant() == "pm";

            // Convert to 24-hour format
            if (hour == 12)
            {
                hour = isPm ? 12 : 0;
            }
            else if (isPm)
            {
                hour += 12;
            }

            return (hour, minute);
        }

        // Try 24-hour format (e.g., "22:00", "14:30")
        var pattern24Hour = @"^(\d{1,2}):(\d{2})$";
        match = Regex.Match(input, pattern24Hour);

        if (match.Success)
        {
            if (!int.TryParse(match.Groups[1].Value, out var hour) || hour < 0 || hour > 23)
            {
                return null;
            }

            if (!int.TryParse(match.Groups[2].Value, out var minute) || minute < 0 || minute > 59)
            {
                return null;
            }

            return (hour, minute);
        }

        return null;
    }
}
