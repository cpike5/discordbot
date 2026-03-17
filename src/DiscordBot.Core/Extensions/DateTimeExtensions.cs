namespace DiscordBot.Core.Extensions;

/// <summary>
/// Extension methods for common <see cref="DateTime"/> and <see cref="DateTimeOffset"/> formatting patterns.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Formats a <see cref="DateTime"/> as an ISO 8601 UTC string.
    /// <see cref="DateTimeKind.Unspecified"/> is treated as UTC.
    /// </summary>
    /// <param name="dt">The datetime value to format.</param>
    /// <returns>An ISO 8601 string with UTC kind, e.g. <c>2024-01-15T12:30:00.0000000Z</c>.</returns>
    public static string ToUtcIso(this DateTime dt)
    {
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("o");
    }

    /// <summary>
    /// Formats a <see cref="DateTime"/> as a Discord timestamp tag.
    /// <see cref="DateTimeKind.Unspecified"/> is treated as UTC.
    /// </summary>
    /// <param name="dt">The datetime value to format.</param>
    /// <param name="style">
    /// The Discord timestamp style. Common values:
    /// <list type="bullet">
    ///   <item><description><c>t</c> — short time (e.g. 9:01 AM)</description></item>
    ///   <item><description><c>T</c> — long time (e.g. 9:01:00 AM)</description></item>
    ///   <item><description><c>d</c> — short date (e.g. 01/01/2024)</description></item>
    ///   <item><description><c>D</c> — long date (e.g. January 1, 2024)</description></item>
    ///   <item><description><c>f</c> — short date/time (e.g. January 1, 2024 9:01 AM)</description></item>
    ///   <item><description><c>F</c> — long date/time (e.g. Monday, January 1, 2024 9:01 AM)</description></item>
    ///   <item><description><c>R</c> — relative (e.g. 2 hours ago) — default</description></item>
    /// </list>
    /// </param>
    /// <returns>A Discord timestamp tag, e.g. <c>&lt;t:1705316400:R&gt;</c>.</returns>
    public static string ToDiscordTimestamp(this DateTime dt, string style = "R")
    {
        var unix = new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeSeconds();
        return $"<t:{unix}:{style}>";
    }

    /// <summary>
    /// Formats a <see cref="DateTimeOffset"/> as a Discord timestamp tag.
    /// </summary>
    /// <param name="dto">The datetime offset value to format.</param>
    /// <param name="style">
    /// The Discord timestamp style. Common values:
    /// <list type="bullet">
    ///   <item><description><c>t</c> — short time (e.g. 9:01 AM)</description></item>
    ///   <item><description><c>T</c> — long time (e.g. 9:01:00 AM)</description></item>
    ///   <item><description><c>d</c> — short date (e.g. 01/01/2024)</description></item>
    ///   <item><description><c>D</c> — long date (e.g. January 1, 2024)</description></item>
    ///   <item><description><c>f</c> — short date/time (e.g. January 1, 2024 9:01 AM)</description></item>
    ///   <item><description><c>F</c> — long date/time (e.g. Monday, January 1, 2024 9:01 AM)</description></item>
    ///   <item><description><c>R</c> — relative (e.g. 2 hours ago) — default</description></item>
    /// </list>
    /// </param>
    /// <returns>A Discord timestamp tag, e.g. <c>&lt;t:1705316400:R&gt;</c>.</returns>
    public static string ToDiscordTimestamp(this DateTimeOffset dto, string style = "R")
    {
        return $"<t:{dto.ToUnixTimeSeconds()}:{style}>";
    }
}
