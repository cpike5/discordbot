using System.Text;
using System.Text.RegularExpressions;

namespace DiscordBot.Bot.Utilities;

/// <summary>
/// Utility class for parsing and formatting duration strings.
/// </summary>
public static partial class DurationParser
{
    private const string DurationPattern = @"(\d+)([smhdw])";

    [GeneratedRegex(DurationPattern, RegexOptions.IgnoreCase)]
    private static partial Regex DurationRegex();

    /// <summary>
    /// Parses a duration string into a TimeSpan.
    /// Supports: s (seconds), m (minutes), h (hours), d (days), w (weeks)
    /// Examples: "10m", "1h30m", "7d", "2w"
    /// </summary>
    /// <param name="input">The duration string to parse</param>
    /// <returns>A TimeSpan if parsing succeeds, null otherwise</returns>
    public static TimeSpan? Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var matches = DurationRegex().Matches(input);
        if (matches.Count == 0)
        {
            return null;
        }

        var totalSeconds = 0.0;

        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups[1].Value, out var value))
            {
                return null;
            }

            var unit = match.Groups[2].Value.ToLowerInvariant();

            totalSeconds += unit switch
            {
                "s" => value,
                "m" => value * 60,
                "h" => value * 3600,
                "d" => value * 86400,
                "w" => value * 604800,
                _ => 0
            };
        }

        // Validate that we have a positive duration
        if (totalSeconds <= 0)
        {
            return null;
        }

        return TimeSpan.FromSeconds(totalSeconds);
    }

    /// <summary>
    /// Formats a TimeSpan into a human-readable string.
    /// Examples: "1 hour 30 minutes", "7 days", "2 weeks"
    /// </summary>
    /// <param name="duration">The TimeSpan to format</param>
    /// <returns>A human-readable string representation</returns>
    public static string Format(TimeSpan duration)
    {
        var parts = new List<string>();

        var weeks = (int)(duration.TotalDays / 7);
        if (weeks > 0)
        {
            parts.Add($"{weeks} {(weeks == 1 ? "week" : "weeks")}");
            duration = duration.Subtract(TimeSpan.FromDays(weeks * 7));
        }

        var days = duration.Days;
        if (days > 0)
        {
            parts.Add($"{days} {(days == 1 ? "day" : "days")}");
        }

        var hours = duration.Hours;
        if (hours > 0)
        {
            parts.Add($"{hours} {(hours == 1 ? "hour" : "hours")}");
        }

        var minutes = duration.Minutes;
        if (minutes > 0)
        {
            parts.Add($"{minutes} {(minutes == 1 ? "minute" : "minutes")}");
        }

        var seconds = duration.Seconds;
        if (seconds > 0 || parts.Count == 0)
        {
            parts.Add($"{seconds} {(seconds == 1 ? "second" : "seconds")}");
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Formats a TimeSpan into a short string.
    /// Examples: "1h30m", "7d", "2w"
    /// </summary>
    /// <param name="duration">The TimeSpan to format</param>
    /// <returns>A compact string representation</returns>
    public static string FormatShort(TimeSpan duration)
    {
        var sb = new StringBuilder();

        var weeks = (int)(duration.TotalDays / 7);
        if (weeks > 0)
        {
            sb.Append($"{weeks}w");
            duration = duration.Subtract(TimeSpan.FromDays(weeks * 7));
        }

        var days = duration.Days;
        if (days > 0)
        {
            sb.Append($"{days}d");
        }

        var hours = duration.Hours;
        if (hours > 0)
        {
            sb.Append($"{hours}h");
        }

        var minutes = duration.Minutes;
        if (minutes > 0)
        {
            sb.Append($"{minutes}m");
        }

        var seconds = duration.Seconds;
        if (seconds > 0 || sb.Length == 0)
        {
            sb.Append($"{seconds}s");
        }

        return sb.ToString();
    }
}
