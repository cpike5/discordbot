namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Provides display-formatting utilities shared across all search providers.
/// </summary>
public static class SearchDisplayHelper
{
    /// <summary>
    /// Returns a human-readable relative-time string for <paramref name="dateTime"/>
    /// relative to <see cref="DateTime.UtcNow"/>.
    /// </summary>
    /// <param name="dateTime">The UTC date/time to describe.</param>
    /// <returns>A relative-time string such as "in 3 minutes" or "2 hours ago".</returns>
    public static string GetRelativeTime(DateTime dateTime)
    {
        var now = DateTime.UtcNow;
        var diff = dateTime - now;

        if (diff.TotalSeconds < 0)
        {
            var absDiff = diff.Negate();

            if (absDiff.TotalMinutes < 1)
                return "less than a minute ago";
            if (absDiff.TotalMinutes < 60)
                return $"{Math.Floor(absDiff.TotalMinutes)} minute{(Math.Floor(absDiff.TotalMinutes) != 1 ? "s" : "")} ago";
            if (absDiff.TotalHours < 24)
                return $"{Math.Floor(absDiff.TotalHours)} hour{(Math.Floor(absDiff.TotalHours) != 1 ? "s" : "")} ago";
            if (absDiff.TotalDays < 7)
                return $"{Math.Floor(absDiff.TotalDays)} day{(Math.Floor(absDiff.TotalDays) != 1 ? "s" : "")} ago";

            return $"on {dateTime:MMM d, yyyy}";
        }

        if (diff.TotalMinutes < 1)
            return "in less than a minute";
        if (diff.TotalMinutes < 60)
            return $"in {Math.Floor(diff.TotalMinutes)} minute{(Math.Floor(diff.TotalMinutes) != 1 ? "s" : "")}";
        if (diff.TotalHours < 24)
            return $"in {Math.Floor(diff.TotalHours)} hour{(Math.Floor(diff.TotalHours) != 1 ? "s" : "")}";
        if (diff.TotalDays < 7)
            return $"in {Math.Floor(diff.TotalDays)} day{(Math.Floor(diff.TotalDays) != 1 ? "s" : "")}";

        return $"on {dateTime:MMM d, yyyy}";
    }

    /// <summary>
    /// Returns the Bootstrap badge variant for a user role string.
    /// </summary>
    /// <param name="role">The role name (e.g. "Admin", "SuperAdmin").</param>
    /// <returns>A Bootstrap badge variant name.</returns>
    public static string GetRoleBadgeVariant(string role) => role switch
    {
        "SuperAdmin" => "danger",
        "Admin"      => "warning",
        "Moderator"  => "info",
        "Viewer"     => "success",
        _            => "secondary"
    };

    /// <summary>
    /// Returns the Bootstrap badge variant for an audit-log category name.
    /// </summary>
    /// <param name="categoryName">The audit log category name.</param>
    /// <returns>A Bootstrap badge variant name.</returns>
    public static string GetAuditLogBadgeVariant(string categoryName) => categoryName switch
    {
        "Security"      => "danger",
        "Configuration" => "warning",
        "Moderation"    => "info",
        "User"          => "primary",
        _               => "secondary"
    };

    /// <summary>
    /// Returns the Bootstrap badge variant for a page section name.
    /// </summary>
    /// <param name="section">The page section label.</param>
    /// <returns>A Bootstrap badge variant name.</returns>
    public static string GetSectionBadgeVariant(string? section) => section switch
    {
        "Main"        => "primary",
        "Guild"       => "success",
        "Admin"       => "warning",
        "Performance" => "info",
        "Account"     => "secondary",
        "Dev"         => "dark",
        _             => "secondary"
    };

    /// <summary>
    /// Truncates <paramref name="text"/> to <paramref name="maxLength"/> characters,
    /// appending <c>...</c> if truncated.
    /// </summary>
    /// <param name="text">The text to truncate.</param>
    /// <param name="maxLength">Maximum character length before truncation.</param>
    /// <returns>The original or truncated string.</returns>
    public static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }
}
