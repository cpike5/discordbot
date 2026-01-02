using System.Text.RegularExpressions;
using System.Web;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Provides methods for highlighting search terms in text.
/// All methods are XSS-safe - text is HTML-encoded before marking.
/// </summary>
public static class TextHighlightHelper
{
    /// <summary>
    /// Highlights matching text with mark tags.
    /// XSS-safe: HTML escapes content before marking.
    /// </summary>
    /// <param name="text">The text to search in</param>
    /// <param name="searchTerm">The term to highlight</param>
    /// <param name="markClass">CSS class for the mark element</param>
    /// <returns>HTML string with highlighted matches</returns>
    public static string HighlightMatches(string? text, string? searchTerm, string markClass = "search-highlight")
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (string.IsNullOrWhiteSpace(searchTerm))
            return HttpUtility.HtmlEncode(text);

        // Escape HTML first (XSS protection)
        var escaped = HttpUtility.HtmlEncode(text);
        var escapedSearchTerm = HttpUtility.HtmlEncode(searchTerm);

        // Case-insensitive replace while preserving original casing
        var pattern = Regex.Escape(escapedSearchTerm);
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);

        return regex.Replace(escaped, match =>
            $"<mark class=\"{HttpUtility.HtmlAttributeEncode(markClass)}\">{match.Value}</mark>");
    }

    /// <summary>
    /// Truncates text to maxLength and highlights matches.
    /// </summary>
    /// <param name="text">The text to truncate and highlight</param>
    /// <param name="searchTerm">The term to highlight</param>
    /// <param name="maxLength">Maximum length before truncation</param>
    /// <param name="ellipsis">String to append when truncated</param>
    /// <returns>HTML string with truncated and highlighted text</returns>
    public static string TruncateAndHighlight(string? text, string? searchTerm, int maxLength = 100, string ellipsis = "...")
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var truncated = text.Length <= maxLength
            ? text
            : text.Substring(0, maxLength) + ellipsis;

        return HighlightMatches(truncated, searchTerm);
    }

    /// <summary>
    /// Highlights matches and ensures the matching portion is visible when truncating.
    /// Useful for long text where the match might be past the truncation point.
    /// </summary>
    /// <param name="text">The text to process</param>
    /// <param name="searchTerm">The term to highlight</param>
    /// <param name="maxLength">Maximum visible length</param>
    /// <param name="contextChars">Characters to show before the match</param>
    /// <returns>HTML string with context around the match</returns>
    public static string HighlightWithContext(string? text, string? searchTerm, int maxLength = 100, int contextChars = 20)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(searchTerm))
            return TruncateAndHighlight(text, searchTerm, maxLength);

        var matchIndex = text.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase);

        // If no match or match is within the visible area, use simple truncation
        if (matchIndex < 0 || matchIndex < maxLength - searchTerm.Length)
            return TruncateAndHighlight(text, searchTerm, maxLength);

        // Start a bit before the match
        var startIndex = Math.Max(0, matchIndex - contextChars);
        var prefix = startIndex > 0 ? "..." : "";

        var endIndex = Math.Min(text.Length, startIndex + maxLength);
        var suffix = endIndex < text.Length ? "..." : "";

        var excerpt = prefix + text.Substring(startIndex, endIndex - startIndex) + suffix;

        return HighlightMatches(excerpt, searchTerm);
    }
}
