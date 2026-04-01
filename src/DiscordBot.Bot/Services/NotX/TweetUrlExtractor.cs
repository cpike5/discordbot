using System.Text.RegularExpressions;

namespace DiscordBot.Bot.Services.NotX;

/// <summary>
/// Extracts X/Twitter tweet URLs from message text using a compiled regular expression.
/// </summary>
public static class TweetUrlExtractor
{
    /// <summary>
    /// Compiled regex matching both twitter.com and x.com status URLs.
    /// Group 1: screen name. Group 2: tweet ID.
    /// </summary>
    private static readonly Regex TweetUrlRegex = new(
        @"https?://(?:www\.)?(?:twitter\.com|x\.com)/(\w+)/status/(\d+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Extracts all tweet URL matches from the provided text.
    /// </summary>
    /// <param name="text">The message text to scan.</param>
    /// <returns>
    /// A read-only list of <see cref="TweetUrlMatch"/> records, one per unique tweet URL found.
    /// Returns an empty list when <paramref name="text"/> is null or empty.
    /// </returns>
    public static IReadOnlyList<TweetUrlMatch> Extract(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<TweetUrlMatch>();

        var matches = TweetUrlRegex.Matches(text);
        if (matches.Count == 0)
            return Array.Empty<TweetUrlMatch>();

        var results = new List<TweetUrlMatch>(matches.Count);
        foreach (Match match in matches)
        {
            results.Add(new TweetUrlMatch(
                ScreenName: match.Groups[1].Value,
                TweetId: match.Groups[2].Value,
                FullUrl: match.Value));
        }

        return results;
    }
}

/// <summary>
/// Represents a single tweet URL extracted from message text.
/// </summary>
/// <param name="ScreenName">The Twitter/X screen name parsed from the URL.</param>
/// <param name="TweetId">The numeric tweet ID parsed from the URL.</param>
/// <param name="FullUrl">The complete URL as it appeared in the message text.</param>
public record TweetUrlMatch(string ScreenName, string TweetId, string FullUrl);
