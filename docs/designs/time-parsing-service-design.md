# Time Parsing Service Design

**Version:** 1.0
**Created:** 2025-12-31
**Status:** Draft
**Feature:** Reminder System - Time Parsing

---

## Overview

This document specifies the `ITimeParsingService` interface and implementation for parsing natural language time inputs into UTC DateTime values. The service supports relative times (e.g., "10m", "2h"), absolute times (e.g., "10pm", "22:00"), named days (e.g., "tomorrow 3pm"), and full date/time formats.

## Design Goals

1. **User-friendly input:** Accept natural language time formats commonly used in Discord communities
2. **Timezone awareness:** Convert all times to UTC based on guild timezone configuration
3. **Explicit error handling:** Return clear error messages for invalid inputs
4. **Type detection:** Track which parse format succeeded for logging and analytics
5. **Reusability:** Service can be used for reminders, scheduled messages, and future time-based features
6. **Validation:** Enforce minimum and maximum advance time constraints

## Existing Pattern Reference

The `RatWatchService.ParseScheduleTime()` method provides a foundation for time parsing. This design extends that pattern into a dedicated, reusable service with improved error handling and format detection.

**Existing implementation location:** `src/DiscordBot.Bot/Services/RatWatchService.cs` (lines 493-650)

---

## Service Interface

### ITimeParsingService

**Location:** `src/DiscordBot.Core/Interfaces/ITimeParsingService.cs`

```csharp
namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for parsing natural language time inputs into UTC DateTime values.
/// Supports relative times, absolute times, and timezone-aware conversions.
/// </summary>
public interface ITimeParsingService
{
    /// <summary>
    /// Parses a time input string into a UTC DateTime.
    /// </summary>
    /// <param name="input">Time input string (e.g., "10m", "tomorrow 3pm", "2024-12-31 10:00").</param>
    /// <param name="timezone">IANA timezone identifier for absolute time conversions.</param>
    /// <returns>Parse result containing UTC time or error message.</returns>
    TimeParseResult Parse(string input, string timezone);

    /// <summary>
    /// Validates that a parsed UTC time is within allowed advance time constraints.
    /// </summary>
    /// <param name="utcTime">The UTC time to validate.</param>
    /// <param name="minAdvanceMinutes">Minimum minutes in the future (default: 1).</param>
    /// <param name="maxAdvanceDays">Maximum days in the future (default: 365).</param>
    /// <returns>Validation result with error message if invalid.</returns>
    ValidationResult ValidateAdvanceTime(
        DateTime utcTime,
        int minAdvanceMinutes = 1,
        int maxAdvanceDays = 365);
}
```

### TimeParseResult

**Location:** `src/DiscordBot.Core/DTOs/TimeParseResult.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a time parsing operation.
/// </summary>
public class TimeParseResult
{
    /// <summary>
    /// Whether the parse operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Parsed UTC timestamp. Null if parsing failed.
    /// </summary>
    public DateTime? UtcTime { get; init; }

    /// <summary>
    /// Error message if parsing failed. Null if successful.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Type of format that was successfully parsed. Null if parsing failed.
    /// </summary>
    public TimeParseType? ParseType { get; init; }

    /// <summary>
    /// Creates a successful parse result.
    /// </summary>
    public static TimeParseResult Ok(DateTime utcTime, TimeParseType type) =>
        new()
        {
            Success = true,
            UtcTime = utcTime,
            ParseType = type
        };

    /// <summary>
    /// Creates a failed parse result with error message.
    /// </summary>
    public static TimeParseResult Error(string message) =>
        new()
        {
            Success = false,
            ErrorMessage = message
        };
}
```

### TimeParseType

**Location:** `src/DiscordBot.Core/Enums/TimeParseType.cs`

```csharp
namespace DiscordBot.Core.Enums;

/// <summary>
/// Type of time format successfully parsed.
/// Used for logging, analytics, and user feedback.
/// </summary>
public enum TimeParseType
{
    /// <summary>
    /// Relative time from now (e.g., "10m", "2h", "1d").
    /// </summary>
    Relative = 0,

    /// <summary>
    /// Absolute time without date (e.g., "10pm", "22:00", "3:30pm").
    /// Assumes today or tomorrow if time has passed.
    /// </summary>
    AbsoluteTime = 1,

    /// <summary>
    /// Named day with time (e.g., "tomorrow 3pm", "monday 9am").
    /// </summary>
    NamedDay = 2,

    /// <summary>
    /// Date with time (e.g., "Dec 31 noon", "Jan 1 9am").
    /// </summary>
    DateWithTime = 3,

    /// <summary>
    /// Full date and time format (e.g., "2024-12-31 10:00", "12/31/2024 10:00 AM").
    /// </summary>
    FullDateTime = 4
}
```

### ValidationResult

**Location:** `src/DiscordBot.Core/DTOs/ValidationResult.cs`

```csharp
namespace DiscordBot.Core.DTOs;

/// <summary>
/// Result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Error message if validation failed. Null if valid.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Ok() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with error message.
    /// </summary>
    public static ValidationResult Error(string message) =>
        new()
        {
            IsValid = false,
            ErrorMessage = message
        };
}
```

---

## Supported Time Formats

### Format Priority Order

The parser attempts formats in this order (first match wins):

1. **Relative time** - Pattern: `^\d+[mhd]$` or `^\d+h\d+m$`
2. **Absolute time (12-hour)** - Pattern: `^\d{1,2}(:\d{2})?\s*(am|pm)$`
3. **Absolute time (24-hour)** - Pattern: `^\d{1,2}:\d{2}$`
4. **Named day + time** - Pattern: `^(tomorrow|monday|tuesday|...)\s+(.+)$`
5. **Date + time** - Pattern: `^(jan|feb|...|december)\s+\d{1,2}\s+(.+)$`
6. **Full date/time** - ISO 8601 or common formats via `DateTime.TryParse`

### Detailed Format Specifications

#### 1. Relative Time (TimeParseType.Relative)

**Examples:**
- `10m` → 10 minutes from now
- `2h` → 2 hours from now
- `1d` → 1 day from now
- `1w` → 1 week from now (7 days)
- `1h30m` → 1 hour 30 minutes from now
- `2h 15m` → 2 hours 15 minutes from now (with space)
- `in 10m` → 10 minutes from now (optional "in" prefix)

**Pattern:** `^(?:in\s+)?(?:(\d+)\s*w(?:eeks?)?)?(?:\s*(\d+)\s*d(?:ays?)?)?(?:\s*(\d+)\s*h(?:ours?)?)?(?:\s*(\d+)\s*m(?:in(?:utes?)?)?)?$`

**Implementation:**
```csharp
private DateTime? ParseRelativeTime(string input)
{
    input = input.Replace("in ", "").Trim();

    var pattern = @"^(?:(\d+)\s*w(?:eeks?)?)?(?:\s*(\d+)\s*d(?:ays?)?)?(?:\s*(\d+)\s*h(?:ours?)?)?(?:\s*(\d+)\s*m(?:in(?:utes?)?)?)?$";
    var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);

    if (!match.Success)
        return null;

    var weeks = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
    var days = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
    var hours = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
    var minutes = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;

    if (weeks == 0 && days == 0 && hours == 0 && minutes == 0)
        return null;

    return DateTime.UtcNow
        .AddDays(weeks * 7)
        .AddDays(days)
        .AddHours(hours)
        .AddMinutes(minutes);
}
```

**Edge Cases:**
- `0m` → Invalid (must be positive)
- `10` → Invalid (must have unit)
- Combinations like `1w2d3h30m` → Valid (all components summed)

#### 2. Absolute Time - 12-Hour Format (TimeParseType.AbsoluteTime)

**Examples:**
- `10pm` → 10:00 PM today/tomorrow
- `10:30pm` → 10:30 PM today/tomorrow
- `3:45am` → 3:45 AM today/tomorrow
- `12am` → Midnight (00:00)
- `12pm` → Noon (12:00)

**Pattern:** `^(\d{1,2})(?::(\d{2}))?\s*(am|pm)$`

**Implementation:**
```csharp
private DateTime? ParseAbsoluteTime12Hour(string input, TimeZoneInfo timeZone)
{
    var pattern = @"^(\d{1,2})(?::(\d{2}))?\s*(am|pm)$";
    var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);

    if (!match.Success)
        return null;

    if (!int.TryParse(match.Groups[1].Value, out var hour) || hour < 1 || hour > 12)
        return null;

    var minute = 0;
    if (match.Groups[2].Success)
    {
        if (!int.TryParse(match.Groups[2].Value, out minute) || minute < 0 || minute > 59)
            return null;
    }

    var isPm = match.Groups[3].Value.Equals("pm", StringComparison.OrdinalIgnoreCase);

    // Convert to 24-hour
    if (hour == 12)
        hour = isPm ? 12 : 0;
    else if (isPm)
        hour += 12;

    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
    var localTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);

    // If time has passed today, schedule for tomorrow
    if (localTime <= now)
        localTime = localTime.AddDays(1);

    return TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);
}
```

**Edge Cases:**
- `13pm` → Invalid (hour must be 1-12)
- `10:60pm` → Invalid (minute must be 0-59)
- `10:5pm` → Invalid (minutes must be zero-padded: `10:05pm`)

#### 3. Absolute Time - 24-Hour Format (TimeParseType.AbsoluteTime)

**Examples:**
- `22:00` → 10:00 PM today/tomorrow
- `14:30` → 2:30 PM today/tomorrow
- `00:00` → Midnight
- `9:00` → 9:00 AM today/tomorrow

**Pattern:** `^(\d{1,2}):(\d{2})$`

**Implementation:**
```csharp
private DateTime? ParseAbsoluteTime24Hour(string input, TimeZoneInfo timeZone)
{
    var pattern = @"^(\d{1,2}):(\d{2})$";
    var match = Regex.Match(input, pattern);

    if (!match.Success)
        return null;

    if (!int.TryParse(match.Groups[1].Value, out var hour) || hour < 0 || hour > 23)
        return null;

    if (!int.TryParse(match.Groups[2].Value, out var minute) || minute < 0 || minute > 59)
        return null;

    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
    var localTime = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);

    // If time has passed today, schedule for tomorrow
    if (localTime <= now)
        localTime = localTime.AddDays(1);

    return TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);
}
```

#### 4. Named Day + Time (TimeParseType.NamedDay)

**Examples:**
- `tomorrow 3pm` → 3:00 PM tomorrow
- `monday 9am` → 9:00 AM next Monday
- `friday 22:00` → 10:00 PM next Friday
- `next thursday 10:30am` → 10:30 AM next Thursday

**Pattern:** `^(tomorrow|monday|tuesday|wednesday|thursday|friday|saturday|sunday)\s+(.+)$`

**Implementation:**
```csharp
private DateTime? ParseNamedDay(string input, TimeZoneInfo timeZone)
{
    var pattern = @"^(tomorrow|monday|tuesday|wednesday|thursday|friday|saturday|sunday)\s+(.+)$";
    var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);

    if (!match.Success)
        return null;

    var dayName = match.Groups[1].Value.ToLowerInvariant();
    var timeInput = match.Groups[2].Value;

    // Parse the time portion recursively
    var timeResult = ParseAbsoluteTime12Hour(timeInput, timeZone)
        ?? ParseAbsoluteTime24Hour(timeInput, timeZone);

    if (!timeResult.HasValue)
        return null;

    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
    var targetDate = now.Date;

    if (dayName == "tomorrow")
    {
        targetDate = targetDate.AddDays(1);
    }
    else
    {
        var targetDayOfWeek = Enum.Parse<DayOfWeek>(dayName, ignoreCase: true);
        var daysUntilTarget = ((int)targetDayOfWeek - (int)now.DayOfWeek + 7) % 7;
        if (daysUntilTarget == 0)
            daysUntilTarget = 7; // Next week's occurrence

        targetDate = targetDate.AddDays(daysUntilTarget);
    }

    var localResult = TimeZoneInfo.ConvertTimeFromUtc(timeResult.Value, timeZone);
    var finalLocal = new DateTime(
        targetDate.Year, targetDate.Month, targetDate.Day,
        localResult.Hour, localResult.Minute, 0);

    return TimeZoneInfo.ConvertTimeToUtc(finalLocal, timeZone);
}
```

**Edge Cases:**
- `monday 99pm` → Invalid (invalid time portion)
- `next monday 3pm` → "next" prefix ignored, treated as "monday 3pm"

#### 5. Date + Time (TimeParseType.DateWithTime)

**Examples:**
- `Dec 31 noon` → December 31 at 12:00 PM
- `Jan 1 9am` → January 1 at 9:00 AM
- `March 15 22:00` → March 15 at 10:00 PM
- `12/31 3pm` → December 31 at 3:00 PM

**Pattern:** `^(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\s+(\d{1,2})\s+(.+)$`

**Implementation:**
```csharp
private DateTime? ParseDateWithTime(string input, TimeZoneInfo timeZone)
{
    var pattern = @"^(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\s+(\d{1,2})\s+(.+)$";
    var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);

    if (!match.Success)
        return null;

    var monthStr = match.Groups[1].Value;
    var day = int.Parse(match.Groups[2].Value);
    var timeInput = match.Groups[3].Value;

    var monthMap = new Dictionary<string, int>
    {
        {"jan", 1}, {"feb", 2}, {"mar", 3}, {"apr", 4},
        {"may", 5}, {"jun", 6}, {"jul", 7}, {"aug", 8},
        {"sep", 9}, {"oct", 10}, {"nov", 11}, {"dec", 12}
    };

    if (!monthMap.TryGetValue(monthStr.ToLowerInvariant(), out var month))
        return null;

    var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
    var year = now.Year;

    // If date has passed this year, use next year
    if (month < now.Month || (month == now.Month && day < now.Day))
        year++;

    try
    {
        var date = new DateTime(year, month, day);

        // Parse time portion
        var timeResult = ParseAbsoluteTime12Hour(timeInput, timeZone)
            ?? ParseAbsoluteTime24Hour(timeInput, timeZone);

        if (!timeResult.HasValue)
            return null;

        var localResult = TimeZoneInfo.ConvertTimeFromUtc(timeResult.Value, timeZone);
        var finalLocal = new DateTime(
            date.Year, date.Month, date.Day,
            localResult.Hour, localResult.Minute, 0);

        return TimeZoneInfo.ConvertTimeToUtc(finalLocal, timeZone);
    }
    catch
    {
        return null; // Invalid date (e.g., Feb 31)
    }
}
```

#### 6. Full Date/Time (TimeParseType.FullDateTime)

**Examples:**
- `2024-12-31 10:00` → ISO 8601 format
- `12/31/2024 10:00 AM` → US date format
- `31/12/2024 22:00` → European date format

**Implementation:**
```csharp
private DateTime? ParseFullDateTime(string input, TimeZoneInfo timeZone)
{
    if (DateTime.TryParse(input, out var parsedLocal))
    {
        // Treat parsed time as local to the specified timezone
        return TimeZoneInfo.ConvertTimeToUtc(parsedLocal, timeZone);
    }

    return null;
}
```

**Note:** This is a catch-all using .NET's built-in parsing. Ambiguous formats may parse incorrectly based on server culture.

---

## Timezone Handling

### Timezone Sources

1. **Guild settings:** Primary source from `GuildRatWatchSettings.Timezone` (or future `GuildSettings.Timezone`)
2. **User preference:** Future enhancement for per-user timezone overrides
3. **Fallback:** UTC if no guild settings exist

### Timezone Validation

```csharp
private TimeZoneInfo GetTimeZone(string timezone)
{
    try
    {
        return TimeZoneInfo.FindSystemTimeZoneById(timezone);
    }
    catch (TimeZoneNotFoundException)
    {
        _logger.LogWarning("Invalid timezone '{Timezone}', falling back to UTC", timezone);
        return TimeZoneInfo.Utc;
    }
}
```

### Supported Timezone Identifiers

Use IANA timezone IDs (e.g., `America/New_York`, `Europe/London`, `UTC`).

**Common timezones:**
- `America/New_York` (Eastern Time)
- `America/Chicago` (Central Time)
- `America/Denver` (Mountain Time)
- `America/Los_Angeles` (Pacific Time)
- `Europe/London` (GMT/BST)
- `UTC` (Coordinated Universal Time)

---

## Validation Rules

### Advance Time Validation

```csharp
public ValidationResult ValidateAdvanceTime(
    DateTime utcTime,
    int minAdvanceMinutes = 1,
    int maxAdvanceDays = 365)
{
    var now = DateTime.UtcNow;
    var minTime = now.AddMinutes(minAdvanceMinutes);
    var maxTime = now.AddDays(maxAdvanceDays);

    if (utcTime < minTime)
    {
        return ValidationResult.Error(
            $"Time must be at least {minAdvanceMinutes} minute(s) in the future");
    }

    if (utcTime > maxTime)
    {
        return ValidationResult.Error(
            $"Time cannot be more than {maxAdvanceDays} day(s) in the future");
    }

    return ValidationResult.Ok();
}
```

### Configuration Defaults

From `ReminderOptions`:
- `MinAdvanceMinutes`: 1 (cannot set reminder for immediate execution)
- `MaxAdvanceDays`: 365 (cannot set reminder more than 1 year out)

---

## Service Implementation

**Location:** `src/DiscordBot.Bot/Services/TimeParsingService.cs`

```csharp
namespace DiscordBot.Bot.Services;

public class TimeParsingService : ITimeParsingService
{
    private readonly ILogger<TimeParsingService> _logger;

    public TimeParsingService(ILogger<TimeParsingService> logger)
    {
        _logger = logger;
    }

    public TimeParseResult Parse(string input, string timezone)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return TimeParseResult.Error("Time input cannot be empty");
        }

        input = input.Trim().ToLowerInvariant();
        var timeZone = GetTimeZone(timezone);

        // Try formats in priority order
        var result = TryParseRelative(input)
            ?? TryParseAbsoluteTime12Hour(input, timeZone)
            ?? TryParseAbsoluteTime24Hour(input, timeZone)
            ?? TryParseNamedDay(input, timeZone)
            ?? TryParseDateWithTime(input, timeZone)
            ?? TryParseFullDateTime(input, timeZone);

        if (result.HasValue)
        {
            var (utcTime, parseType) = result.Value;
            _logger.LogDebug(
                "Parsed '{Input}' as {ParseType} to UTC: {UtcTime}",
                input, parseType, utcTime);
            return TimeParseResult.Ok(utcTime, parseType);
        }

        _logger.LogDebug("Failed to parse time input: '{Input}'", input);
        return TimeParseResult.Error(
            "Could not understand time format. Try: '10m', '2h', '3pm', 'tomorrow 9am', 'Dec 31 noon'");
    }

    public ValidationResult ValidateAdvanceTime(
        DateTime utcTime,
        int minAdvanceMinutes = 1,
        int maxAdvanceDays = 365)
    {
        // Implementation as shown above
    }

    // Private parsing methods...
}
```

---

## Error Messages

### User-Facing Error Messages

| Scenario | Error Message |
|----------|---------------|
| Empty input | "Time input cannot be empty" |
| Invalid format | "Could not understand time format. Try: '10m', '2h', '3pm', 'tomorrow 9am', 'Dec 31 noon'" |
| Too soon | "Time must be at least 1 minute(s) in the future" |
| Too far | "Time cannot be more than 365 day(s) in the future" |
| Invalid timezone | "Invalid timezone '{timezone}', falling back to UTC" (logged, not returned) |
| Invalid date | "Could not understand time format..." (invalid date like Feb 31 fails silently) |

---

## Testing Considerations

### Unit Test Cases

**Test class:** `TimeParsingServiceTests.cs`

```csharp
[Theory]
[InlineData("10m", 10)] // 10 minutes from now
[InlineData("2h", 120)] // 2 hours = 120 minutes
[InlineData("1d", 1440)] // 1 day = 1440 minutes
[InlineData("1h30m", 90)] // 1 hour 30 minutes
public void Parse_RelativeTime_ReturnsCorrectUtcTime(string input, int expectedMinutesFromNow)
{
    // Arrange
    var service = new TimeParsingService(_logger);
    var before = DateTime.UtcNow;

    // Act
    var result = service.Parse(input, "UTC");

    // Assert
    Assert.True(result.Success);
    Assert.Equal(TimeParseType.Relative, result.ParseType);
    var expectedTime = before.AddMinutes(expectedMinutesFromNow);
    Assert.True(Math.Abs((result.UtcTime.Value - expectedTime).TotalSeconds) < 5);
}

[Theory]
[InlineData("10pm", 22, 0)]
[InlineData("10:30pm", 22, 30)]
[InlineData("3:45am", 3, 45)]
[InlineData("12am", 0, 0)]
[InlineData("12pm", 12, 0)]
public void Parse_AbsoluteTime12Hour_ReturnsCorrectTime(
    string input, int expectedHour, int expectedMinute)
{
    // Test implementation
}

[Fact]
public void Parse_InvalidInput_ReturnsError()
{
    var service = new TimeParsingService(_logger);
    var result = service.Parse("invalid", "UTC");

    Assert.False(result.Success);
    Assert.Contains("Could not understand time format", result.ErrorMessage);
}
```

### Integration Test Cases

Test with actual guild timezone configurations:
- Pacific Time (`America/Los_Angeles`)
- Eastern Time (`America/New_York`)
- UTC
- Invalid timezone (should fall back to UTC)

---

## Future Considerations

1. **Localization:** Support non-English day/month names
2. **Smart parsing:** "in 5 minutes", "in an hour", "at noon", "at midnight"
3. **Range parsing:** "between 3pm and 5pm" (for fuzzy reminders)
4. **Duration parsing:** "for 2 hours" (for temporary settings)
5. **Natural language library:** Consider integrating Chronic.NET or similar
6. **User feedback:** Show parsed result back to user for confirmation ("Reminder set for Dec 31, 2024 at 3:00 PM PST")

---

## Related Documentation

- [Reminder Entity Design](reminder-entity-design.md) - Entity and repository design
- [Reminder Execution Service Design](reminder-execution-design.md) - Background service implementation
- RatWatch time parsing (existing reference implementation)

---

## Dependencies

**NuGet Packages:**
- System.Text.RegularExpressions (framework)
- Microsoft.Extensions.Logging (existing)

**Existing Systems:**
- Guild timezone configuration (from RatWatch or future global settings)
- ILogger<T> for diagnostic logging

---

## Acceptance Criteria

- [ ] ITimeParsingService interface defined
- [ ] TimeParseResult, TimeParseType, ValidationResult classes created
- [ ] TimeParsingService implementation completed
- [ ] All six parse format types supported
- [ ] Timezone conversion tested with IANA identifiers
- [ ] Validation logic enforces min/max advance time
- [ ] Comprehensive unit tests (20+ test cases covering all formats)
- [ ] Integration tests with real timezone conversions
- [ ] Error messages are user-friendly
- [ ] Service registered in DI container
