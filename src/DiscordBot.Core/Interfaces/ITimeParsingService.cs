namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for parsing time expressions into UTC DateTime values.
/// Supports relative times, absolute times, day names, dates, and special keywords.
/// </summary>
public interface ITimeParsingService
{
    /// <summary>
    /// Parses a time expression into a UTC DateTime.
    /// </summary>
    /// <param name="input">The time expression to parse (e.g., "10m", "tomorrow 3pm", "Dec 31", "noon").</param>
    /// <param name="timezone">The IANA timezone ID to use for absolute time parsing (e.g., "America/New_York").</param>
    /// <returns>A TimeParseResult containing the parsed UTC time or error information.</returns>
    TimeParseResult Parse(string input, string timezone);
}

/// <summary>
/// Result of a time parsing operation.
/// </summary>
public class TimeParseResult
{
    /// <summary>
    /// Gets a value indicating whether the parse operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the parsed UTC DateTime if successful; otherwise, null.
    /// </summary>
    public DateTime? UtcTime { get; init; }

    /// <summary>
    /// Gets the error message if unsuccessful; otherwise, null.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the type of time expression that was parsed, if successful.
    /// </summary>
    public TimeParseType? ParseType { get; init; }

    /// <summary>
    /// Creates a successful parse result.
    /// </summary>
    /// <param name="utcTime">The parsed UTC DateTime.</param>
    /// <param name="type">The type of time expression that was parsed.</param>
    /// <returns>A successful TimeParseResult.</returns>
    public static TimeParseResult Ok(DateTime utcTime, TimeParseType type) =>
        new() { Success = true, UtcTime = utcTime, ParseType = type };

    /// <summary>
    /// Creates an error parse result.
    /// </summary>
    /// <param name="message">The error message describing why parsing failed.</param>
    /// <returns>An error TimeParseResult.</returns>
    public static TimeParseResult Error(string message) =>
        new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// Represents the type of time expression that was parsed.
/// </summary>
public enum TimeParseType
{
    /// <summary>
    /// Relative time offset from now (e.g., "10m", "2h", "1d", "1h30m").
    /// </summary>
    Relative,

    /// <summary>
    /// Absolute time of day (e.g., "10pm", "22:00", "14:30").
    /// </summary>
    AbsoluteTime,

    /// <summary>
    /// Day name with optional time (e.g., "tomorrow", "monday", "next friday 3pm").
    /// </summary>
    AbsoluteDay,

    /// <summary>
    /// Month and day with optional time (e.g., "Dec 31", "Jan 1", "january 15 10pm").
    /// </summary>
    AbsoluteDate,

    /// <summary>
    /// Full date and time (e.g., "2024-12-31 10:00", "2025-01-01T14:30").
    /// </summary>
    FullDateTime
}
